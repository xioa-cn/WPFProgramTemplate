# CSharpScriptCore API 与使用手册

`CSharpScriptCore` 是基于 Roslyn 的 .NET 8 CSX 执行库，支持代码字符串、CSX 文件、递归 `#load`、NuGet 引用、静态方法调用、脚本类实例、结构化诊断、编译缓存，以及独立 Worker 进程中的硬超时和 Console I/O。

## 本地项目引用与命名空间

请在宿主项目的 `.csproj` 中直接引用本地源码项目：

```xml
<ItemGroup>
  <ProjectReference Include="..\CSharpScriptCore\CSharpScriptCore.csproj" />
</ItemGroup>
```

请根据宿主项目所在目录调整相对路径。这样构建宿主项目时，`CSharpScriptCore` 会一起编译并复制到宿主输出目录。

常用命名空间：

```csharp
using CSharpScriptCore;
using CSharpScriptCore.Core;
using CSharpScriptCore.Models;
using CSharpScriptCore.Runtime;
```

项目要求 .NET 8。执行 `#r "nuget: ..."` 时，运行机器还必须能执行 `dotnet restore` 并访问相应包源。

## 应该选进程内还是 Worker

| 场景 | 建议入口 | 原因 |
|---|---|---|
| 可信脚本、低启动延迟、高频执行 | `CSharpScript`、`CSharpScriptRun`、`CSharpScriptService` | 进程内编译缓存可复用 |
| 可能死循环、同步阻塞或忽略取消 | `CSharpScriptWorker` | 超时后会杀死整个 Worker 进程树 |
| 需要 `Console.ReadLine/WriteLine` | `CSharpScriptWorker` | 标准输入、输出和错误流均重定向 |
| 需要长期保存并反复调用类实例 | `CSharpScriptService` | Worker 当前是一次请求一个进程，不保存实例会话 |
| 需要执行后可靠卸载脚本程序集 | `CSharpScriptWorker` | Worker 退出即释放该进程加载的程序集 |

`ScriptExecutionOptions.Timeout` 是进程内软超时：它触发取消令牌，但无法强制停止不合作的同步代码。`ScriptWorkerOptions.Timeout` 是硬超时：到期后终止 Worker 及其子进程。对于不可信代码，只使用 Worker 仍不等于完整安全沙箱；生产环境还应使用低权限账户、文件系统/网络隔离、资源配额和操作系统级容器或沙箱。

## 1. CSharpScript：执行代码与文件

### 执行代码字符串

```csharp
var result = await CSharpScript.ExecuteCodeAsync<int>("21 * 2");
if (result.Success)
{
    Console.WriteLine(result.Data); // 42
}
else
{
    Console.WriteLine(result.Output);
}
```

同步入口为 `ExecuteByCode(...)` 和 `ExecuteByCode<T>(...)`。异步入口：

```csharp
Task<ScriptResult> ExecuteCodeAsync(string code, ScriptExecutionOptions? options = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult<T>> ExecuteCodeAsync<T>(string code, ScriptExecutionOptions? options = null,
    CancellationToken cancellationToken = default)
```

`T` 对应脚本最后一个表达式的类型。无泛型版本仍可通过 `ReturnValue` 读取原始返回值。

### 直接执行 CSX 文件

```csharp
var result = await CSharpScript.ExecuteFileAsync<string>(@"D:\scripts\Main.csx");
```

同步入口为 `ExecuteByFile(...)` 和 `ExecuteByFile<T>(...)`；异步入口为 `ExecuteFileAsync(...)` 和 `ExecuteFileAsync<T>(...)`。文件入口会自动设置 `ScriptPath` 和 `BaseDirectory`，因此相对 `#load` 从当前 CSX 所在目录解析。

### 只编译，不执行

```csharp
var result = await CSharpScript.CompileFileAsync(@"D:\scripts\Main.csx");
foreach (var diagnostic in result.StructuredDiagnostics)
{
    Console.WriteLine($"{diagnostic.FilePath}({diagnostic.StartLine},{diagnostic.StartColumn}): " +
                      $"{diagnostic.Id} {diagnostic.Message}");
}
```

可用入口：

```csharp
Task<ScriptResult> CompileCodeAsync(string code, ScriptExecutionOptions? options = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult> CompileFileAsync(string filePath, ScriptExecutionOptions? options = null,
    CancellationToken cancellationToken = default)
```

### Globals 全局对象

```csharp
public sealed class HostGlobals
{
    public required string UserName { get; init; }
    public int Add(int left, int right) => left + right;
}

var options = new ScriptExecutionOptions
{
    Globals = new HostGlobals { UserName = "operator" }
};
var result = await CSharpScript.ExecuteCodeAsync<string>(
    "$\"{UserName}: {Add(20, 22)}\"", options);
```

全局对象的公开成员可在脚本顶层直接使用。`Globals` 只能用于进程内执行，不能序列化到 Worker。

### 取消、Dump 与软超时

```csharp
using var stop = new CancellationTokenSource();
var options = new ScriptExecutionOptions { Timeout = TimeSpan.FromSeconds(5) };

var result = await CSharpScript.ExecuteCodeAsync<int>("""
    ScriptExecution.CancellationToken.ThrowIfCancellationRequested();
    42.Dump("answer")
    """, options, stop.Token);

foreach (var dump in result.Dumps)
    Console.WriteLine($"{dump.Title}: {dump.Value}");
```

脚本可通过 `ScriptExecution.CancellationToken` 或 `ScriptRuntime.CancellationToken` 获得当前令牌。脚本应主动检查令牌，或把它传给支持取消的异步 API。任意值调用 `value.Dump(title)` 后会被加入结果的 `Dumps`，同时原样返回该值。

### 编译缓存

相同代码、引用、导入、依赖和 Globals 类型会复用进程内 Roslyn 编译结果，缓存最多保留 64 项。

```csharp
Console.WriteLine(CSharpScript.CompilationCacheCount);
CSharpScript.ClearCompilationCache();

var options = new ScriptExecutionOptions { EnableCompilationCache = false };
var result = await CSharpScript.ExecuteCodeAsync("1 + 1", options);
```

缓存保存编译对象，不代表脚本运行实例。修改脚本或相关选项会生成不同缓存键。若需要可靠释放所有脚本程序集，请使用一次性 Worker 进程。

### 其他 CSharpScript API

```csharp
ScriptExecutionOptions CreateOptions(string? scriptPath = null)
bool UsesFramework(string code, string frameworkName)
int CompilationCacheCount { get; }
void ClearCompilationCache()
```

`CreateOptions` 根据 CSX 路径初始化目录；`UsesFramework` 检测代码中是否存在指定的 `#r "framework: 名称"` 指令。

## 2. CSX 依赖：#load、NuGet、Framework

支持下面的文件结构，并会递归处理嵌套脚本中的指令：

```csharp
// Main.csx
#r "nuget: HslCommunication, 12.9.2"
#r "framework: Console"
#load "TextClass.csx";
#load "Method.csx";

Run();
```

嵌套文件还可以继续使用 `#load`、`#r "nuget: ..."` 和 `#r "framework: ..."`。每一级相对路径都以声明该 `#load` 的文件目录为基准，并检测循环加载。展开时会写入 `#line` 映射，因此结构化诊断仍能定位到原始文件。

### NuGet 与私有源

```csharp
var options = CSharpScript.CreateOptions(@"D:\scripts\Main.csx");
options.NuGetSources.Add("https://api.nuget.org/v3/index.json");
options.NuGetSources.Add("https://packages.example.com/v3/index.json");
options.NuGetConfigFile = @"D:\config\NuGet.Config";
options.RuntimeIdentifier = "win-x64";

var result = await CSharpScript.ExecuteFileAsync(@"D:\scripts\Main.csx", options);
```

认证信息应放在 `NuGet.Config` 或 NuGet 凭据提供程序中，不要写进 CSX 或日志。`RuntimeIdentifier` 用于还原对应 RID 的托管运行时资产；不设置时自动使用当前进程 RID，显式设置时以设置值为准。当前实现不承诺任意 NuGet 包中的原生库都能自动完成探测和加载；带复杂原生依赖的包应在目标平台单独验证。

## 3. CSharpScriptRun：调用静态方法

```csharp
const string code = """
    public static class MathScript
    {
        public static int Sum(params int[] values) => values.Sum();
    }
    """;

var result = await CSharpScriptRun.RunStaticMethodAsync<int>(
    code,
    "MathScript",
    "Sum",
    parameters: [10, 20, 12]);
```

从文件调用：

```csharp
var result = await CSharpScriptRun.RunStaticMethodFromFileAsync<int>(
    @"D:\scripts\Math.csx",
    "My.Namespace.MathScript",
    "Sum",
    options: new ScriptExecutionOptions(),
    parameters: [1, 2, 3],
    cancellationToken: cancellationToken);
```

公开入口：

```csharp
ScriptResult RunStaticMethod(string code, string className, string methodName, params object[] parameters)
ScriptResult<T> RunStaticMethod<T>(string code, string className, string methodName, params object[] parameters)
Task<ScriptResult> RunStaticMethodAsync(string code, string className, string methodName,
    ScriptExecutionOptions? options = null, object[]? parameters = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult<T>> RunStaticMethodAsync<T>(/* 同上 */)

ScriptResult RunStaticMethodFromFile(string filePath, string className, string methodName,
    params object[] parameters)
ScriptResult<T> RunStaticMethodFromFile<T>(/* 同上 */)
Task<ScriptResult> RunStaticMethodFromFileAsync(/* filePath/className/methodName/options/parameters/token */)
Task<ScriptResult<T>> RunStaticMethodFromFileAsync<T>(/* 同上 */)
```

`RunStaticMethodByFile` 和 `RunStaticMethodByFile<T>` 是同步文件入口的兼容别名。`StaticMethodGlobals` 是实现调用桥接所需的公开但隐藏于 IntelliSense 的类型，业务代码不应直接创建。

## 4. CSharpScriptService：创建并调用脚本实例

```csharp
const string code = """
    public sealed class Counter : IDisposable
    {
        private int _value;
        public Counter(int initial) => _value = initial;
        public int Add(int value = 1) => _value += value;
        public Task<int> AddAsync(int value, CancellationToken cancellationToken) =>
            Task.FromResult(_value += value);
        public void Dispose() { }
    }
    """;

await using var service = await CSharpScriptService.GetServiceFromClassAsync<object>(
    code,
    "Counter",
    args: [10]);

if (!service.Success)
    throw service.Exception ?? new InvalidOperationException(service.Output);

var result = await service.ScriptClassExecuteMethodAsync<object, int>(
    "AddAsync",
    args: [32],
    cancellationToken: cancellationToken);
Console.WriteLine(result.Data); // 42
```

从文件创建实例：

```csharp
await using var service = await CSharpScriptService.GetServiceFromFileAsync<object>(
    @"D:\scripts\Counter.csx", "Counter", args: [10]);
```

创建实例 API：

```csharp
ScriptClass<T> GetServiceFromClass<T>(string code, string className, params object[] args)
Task<ScriptClass<T>> GetServiceFromClassAsync<T>(string code, string className,
    ScriptExecutionOptions? options = null, object[]? args = null,
    CancellationToken cancellationToken = default)
ScriptClass<T> GetServiceFromFile<T>(string filePath, string className, params object[] args)
Task<ScriptClass<T>> GetServiceFromFileAsync<T>(string filePath, string className,
    ScriptExecutionOptions? options = null, object[]? args = null,
    CancellationToken cancellationToken = default)
```

`GetServiceFromClassByFile<T>` 是同步文件入口的兼容别名。`ScriptClassFactoryGlobals` 是实现构造函数绑定的公开但隐藏于 IntelliSense 的桥接类型，不应直接使用。

实例调用 API：

```csharp
ScriptResult<TResult> ScriptClassExecuteMethod<T, TResult>(
    this ScriptClass<T> scriptClass, string methodName, params object[] args)
Task<ScriptResult<TResult>> ScriptClassExecuteMethodAsync<T, TResult>(
    this ScriptClass<T> scriptClass, string methodName, object[]? args = null,
    CancellationToken cancellationToken = default)
IReadOnlyList<ScriptMethodDescriptor> GetPublicMethods<T>(this ScriptClass<T> scriptClass)
```

`ScriptClass<T>` 实现 `IDisposable` 和 `IAsyncDisposable`。释放时会优先调用脚本实例的 `IAsyncDisposable.DisposeAsync()`，同步释放则调用 `IDisposable.Dispose()`。释放实例不等于卸载进程内 Roslyn 程序集。

### 方法和构造函数绑定规则

绑定器支持公开构造函数、公开静态/实例方法、重载、可选参数、`params`、常用数值转换、字符串到枚举、可空值、基础泛型类型推断，以及在调用方未提供时为末尾 `CancellationToken` 注入当前令牌。返回的 `Task`、`Task<T>`、`ValueTask`、`ValueTask<T>` 会被异步展开。

当两个候选项得分相同会返回 `AmbiguousMatchException`；没有候选项会返回 `MissingMethodException`。泛型推断只覆盖从实际参数类型能够直接推断的常见形式，不是完整的 C# 编译器重载解析。绑定器能准备 `ref/out` 调用参数，但当前结果模型不会把修改后的参数数组回传给调用方，因此不要依赖可观察的 `ref/out` 输出。

`GetPublicMethods` 返回 `ScriptMethodDescriptor`，其字段为 `Name`、`ReturnType`、`IsStatic`、`IsGeneric`、`Parameters`。每个 `ScriptParameterDescriptor` 包含 `Name`、`Type`、`IsOptional`、`IsParams`、`IsOut`、`IsByRef`。

## 5. CSharpScriptWorker：独立进程与硬超时

Worker 不是单元测试替代品。测试项目只验证行为；生产宿主必须启动另一个进程，才能在死循环时强制终止、隔离全局 Console，并通过进程退出可靠释放程序集。

### 部署 Worker

发布 Worker 项目，把发布目录中的 `CSharpScriptCore.Worker.exe`、`.dll` 及全部依赖一起部署：

```powershell
dotnet publish CSharpScriptCore.Worker/CSharpScriptCore.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/worker
```

然后显式配置发布后的 Worker 路径：

```csharp
var options = new ScriptWorkerOptions
{
    WorkerPath = Path.GetFullPath(@"artifacts\worker\CSharpScriptCore.Worker.exe")
};
```

也可以把 Worker 发布目录中的全部文件复制到宿主输出目录。此时 `WorkerPath` 可以留空，程序会自动查找宿主目录中的 `CSharpScriptCore.Worker.exe` 或 `CSharpScriptCore.Worker.dll`。不要只复制一个 DLL，Worker 还依赖同目录中的 `.deps.json`、`.runtimeconfig.json`、Core 和 Roslyn 程序集。

### Console 输入输出和硬超时

```csharp
var stdout = string.Empty;
var stderr = string.Empty;
var worker = new ScriptWorkerOptions
{
    WorkerPath = @"D:\tools\CSharpScriptCore.Worker.exe",
    Timeout = TimeSpan.FromSeconds(10),
    ConsoleInput = $"CsxPad{Environment.NewLine}",
    StandardOutputReceived = text => stdout = text,
    StandardErrorReceived = text => stderr = text,
    ExecutionOptions = new ScriptExecutionOptions
    {
        RuntimeIdentifier = "win-x64"
    }
};

var result = await CSharpScriptWorker.ExecuteCodeAsync<string>("""
    #r "framework: Console"
    var name = Console.ReadLine();
    Console.WriteLine($"Hello, {name}");
    name
    """, worker, cancellationToken);
```

`ConsoleInput` 会一次写入标准输入，然后关闭输入流。输出/错误回调在进程结束并完整读取流后调用，不是逐行实时事件。即使没有桌面 Console 窗口，脚本也能使用重定向后的 `Console`。测试运行器默认不会弹 Console 窗口，这是正常行为。

Worker 公开 API：

```csharp
Task<ScriptResult> ExecuteCodeAsync(string code, ScriptWorkerOptions? options = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult<T>> ExecuteCodeAsync<T>(/* 同上 */)
Task<ScriptResult> ExecuteFileAsync(string filePath, ScriptWorkerOptions? options = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult<T>> ExecuteFileAsync<T>(/* 同上 */)
Task<ScriptResult<T>> RunStaticMethodAsync<T>(string code, string className, string methodName,
    object[]? parameters = null, ScriptWorkerOptions? options = null,
    CancellationToken cancellationToken = default)
Task<ScriptResult<T>> RunStaticMethodFromFileAsync<T>(string filePath, string className,
    string methodName, object[]? parameters = null, ScriptWorkerOptions? options = null,
    CancellationToken cancellationToken = default)
```

Worker 参数和返回值通过 JSON 跨进程传输，应为可序列化类型。Worker 每次调用都会启动新进程，因此其进程内编译缓存只在该次请求期间有意义。Worker 当前不提供 `CSharpScriptService` 的长生命周期实例调用。

远端异常在宿主中表现为 `ScriptWorkerException`；`RemoteType` 保存 Worker 内原始异常类型名，`Message` 保存错误消息，远端堆栈不会被还原成本地异常堆栈。

## 6. 选项模型

### ScriptExecutionOptions

| 属性 | 默认值 | 说明 |
|---|---|---|
| `ScriptPath` | `null` | 当前脚本路径；决定顶层相对 `#load` 的目录 |
| `BaseDirectory` | 当前工作目录 | 未指定 `ScriptPath` 时的基础目录 |
| `PackageWorkspaceDirectory` | `AppContext.BaseDirectory/Data/NuGet` | 生成临时还原项目和资产文件的位置 |
| `References` | 空集合 | 附加程序集文件路径 |
| `Imports` | 空集合 | 附加默认命名空间 |
| `Globals` | `null` | 进程内脚本全局对象 |
| `IncludeLoadedAssemblies` | `true` | 自动引用宿主已加载程序集 |
| `EnableNuGetDirectives` | `true` | 是否处理 `#r "nuget: ..."` |
| `NuGetSources` | 空集合 | 附加 NuGet 源 |
| `NuGetConfigFile` | `null` | 指定私有源/认证配置文件 |
| `RuntimeIdentifier` | 当前进程 RID | 还原 RID，例如 `win-x64`；显式设置会覆盖自动检测 |
| `EnableCompilationCache` | `true` | 是否使用进程内编译缓存 |
| `Timeout` | `null` | 进程内软超时 |

集合属性使用 `Add` 添加内容，不能整体赋值。

### ScriptWorkerOptions

| 属性 | 默认值 | 说明 |
|---|---|---|
| `WorkerPath` | `null` | Worker `.exe` 或 `.dll`；空时从宿主目录查找 |
| `Timeout` | 30 秒 | 硬超时，超时后杀死进程树 |
| `ConsoleInput` | 空字符串 | 一次性标准输入 |
| `StandardOutputReceived` | `null` | 完整标准输出回调 |
| `StandardErrorReceived` | `null` | 完整标准错误回调 |
| `EnvironmentVariables` | 空集合 | 传给 Worker 的环境变量 |
| `ExecutionOptions` | 新实例 | Worker 内部的依赖和编译选项；不支持 `Globals` |

## 7. 结果、诊断与状态

### ScriptResult 和 ScriptResult<T>

`ScriptResult` 的公开属性：

| 属性 | 说明 |
|---|---|
| `RunResult` | `ScriptRunStatus` 最终状态 |
| `Success` | 状态是否为 `Success` |
| `Exception` | 编译、运行或宿主异常 |
| `ElapsedMilliseconds` | 总耗时兼容字段 |
| `Diagnostics` | Roslyn 诊断文本 |
| `StructuredDiagnostics` | 带位置的结构化诊断 |
| `Output` | 依赖、编译、运行或 Worker 输出 |
| `ReturnValue` | 原始返回值 |
| `Dumps` | `Dump()` 收集项 |
| `Timings` | 分阶段耗时 |

`ScriptResult<T>` 增加 `Data`。`ScriptClass<T>` 继承它并实现同步/异步释放。`ScriptDump` 是包含 `Value` 和 `Title` 的记录。

`ScriptDiagnostic` 字段：`Id`、`Severity`、`Message`、`FilePath`、`StartLine`、`StartColumn`、`EndLine`、`EndColumn`。行列使用便于展示的一基编号；无源码位置时为 `null`。

`ScriptExecutionTimings` 字段：`DependencyMilliseconds`、`CompilationMilliseconds`、`ExecutionMilliseconds`、`TotalMilliseconds`。

### ScriptRunStatus

| 状态 | 含义 |
|---|---|
| `Idle` | 尚未执行 |
| `LoadingSource` | 正在加载源码 |
| `Compiling` | 正在编译 |
| `ReadyToRun` | 编译完成待运行 |
| `Running` | 正在运行 |
| `Success` | 成功 |
| `Warning` | 成功但有业务警告 |
| `CompileFailed` | Roslyn 编译失败 |
| `RuntimeException` | 脚本或方法运行异常 |
| `Timeout` | 超时 |
| `Cancelled` | 调用方取消 |
| `FileLoadError` | CSX 文件读取失败 |
| `DependencyLoadFailed` | `#load`、NuGet 或依赖准备失败 |
| `HostInternalError` | 执行宿主或 Worker 协议错误 |

调用方应首先判断 `Success`/`RunResult`，再读取 `Data`。不要仅通过 `Data == null` 判断失败，因为合法脚本可以返回 `null`。

## 8. 完整文件示例

目录：

```text
scripts/
  Main.csx
  TextClass.csx
  Method.csx
```

`TextClass.csx`：

```csharp
public sealed record TextClass(string Text);
```

`Method.csx`：

```csharp
#r "nuget: HslCommunication, 12.9.2"

public static class Method
{
    public static string Execute(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return $"{text}: {typeof(HslCommunication.ModBus.ModbusTcpNet).Name}";
    }
}
```

`Main.csx`：

```csharp
#r "framework: Console"
#load "TextClass.csx";
#load "Method.csx";

var model = new TextClass("ready");
Console.WriteLine(model.Text);
Method.Execute(model.Text, ScriptExecution.CancellationToken)
```

宿主：

```csharp
var path = Path.GetFullPath(@"scripts\Main.csx");
var options = new ScriptWorkerOptions
{
    WorkerPath = Path.Combine(AppContext.BaseDirectory, "CSharpScriptCore.Worker.dll"),
    Timeout = TimeSpan.FromSeconds(20),
    ExecutionOptions = CSharpScript.CreateOptions(path)
};
options.ExecutionOptions.RuntimeIdentifier = "win-x64";

var result = await CSharpScriptWorker.ExecuteFileAsync<string>(path, options);
if (!result.Success)
{
    foreach (var diagnostic in result.StructuredDiagnostics)
        Console.Error.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
    throw result.Exception ?? new InvalidOperationException(result.Output);
}

Console.WriteLine(result.Data); // ready: ModbusTcpNet
```

## 本地构建与部署

构建 Core：

```powershell
dotnet build CSharpScriptCore/CSharpScriptCore.csproj -c Release
```

发布 Worker：

```powershell
dotnet publish CSharpScriptCore.Worker/CSharpScriptCore.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/worker
```

Worker 独立部署是必要的运行边界；把相同代码放进测试项目只能验证功能，无法让其他宿主应用获得硬超时、进程退出卸载和独立 Console I/O。更新 Core 后应重新发布 Worker，保证两边使用同一份协议和程序集。
