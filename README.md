# MachineApplication

基于 **.NET 10、WPF 和 MaterialDesignInXamlToolkit** 的模块化桌面应用模板。项目将模块加载、依赖注入、区域导航、主题、多语言、权限和日志整合为一套基础框架，业务页面可在此基础上扩展。

> 当前仓库以基础设施和组件演示为主，不是完整的生产业务系统。WPF 界面仅面向 Windows。

## 主要功能

| 模块 | 已实现能力 |
| --- | --- |
| 应用启动 | 登录后进入主窗口；支持欢迎页与自动打开首个可访问页面 |
| 模块组织 | `modules.xml` 模块配置、模块生命周期、MSDI 服务注册与模块子容器 |
| 页面导航 | URL 路由、区域导航、页面复用、顶部页签、浮动窗口和导航遮挡动画 |
| 界面主题 | MaterialDesign 3、明暗主题、主题色调整、主题配置保存 |
| 多语言 | JSON 语言资源、源生成器生成强类型访问属性、运行时切换、动态语言下拉菜单 |
| 访问控制 | 本地账号、权限等级、页面访问权限、无权菜单隐藏、按钮级权限配置 |
| 消息反馈 | 窗口内及桌面全局 Growl、LoadingBar、Snackbar |
| 日志 | 统一 `GlobalLogger`、可选 Serilog / NLog、按天目录、文件滚动、保留天数 |
| 数据访问 | EF Core + SQLite 权限数据库；EF SQL 执行日志接入统一日志 |
| 运行配置 | `SuperPage` 编辑启动与日志配置，打开或关闭调试控制台 |
| 开发辅助 | 模块项目生成工具、Dispatcher 辅助类、`Result` / `Option` / `Unit` |

## 技术组成

- **桌面界面**：WPF、MaterialDesignThemes。
- **MVVM**：CommunityToolkit.Mvvm。
- **容器与配置**：Microsoft.Extensions.DependencyInjection、Configuration、Options。
- **数据存储**：Entity Framework Core、SQLite。
- **日志实现**：Serilog、NLog，以及项目自有的 DebuggerLogger。
- **代码生成**：模块相关生成器、多语言源生成器。
- **测试项目**：xUnit；分别覆盖部分应用功能与语言生成器。

具体依赖版本以各项目的 `.csproj` 为准。

## 目录结构

```text
仓库根目录/
├─ README.md
├─ LICENSE
└─ MachineApplication/
   ├─ MachineApplication.slnx
   ├─ MachineApplication.Startup/       # 可执行启动项目
   │  ├─ Program.cs
   │  ├─ appSettings.json              # 日志及启动配置
   │  ├─ Router.json                   # 菜单、路由展示与页面等级权限
   │  └─ modules.xml                   # 模块加载配置
   ├─ MachineApplication.Create/       # 交互式模块项目生成工具
   ├─ src/
   │  ├─ Machine.ModuleLoad/            # 模块、导航、日志、权限及公共工具
   │  ├─ MachineApplication.Entrance/   # 主窗口、配置页面、主题及组件
   │  ├─ I18nExtensions/               # 语言管理与资源基类
   │  ├─ I18n.LangsGenerator/           # JSON 语言资源源生成器
   │  ├─ ModuleLoadGenerator/           # 模块相关代码生成
   │  ├─ ModuleLoadSources/             # 模块相关支持类型
   │  └─ RestSharp/                     # 本地 Result / Option / Unit 工具项目
   └─ tests/
      ├─ MachineApplication.Entrance.Tests/
      └─ I18n.LangsGenerator.Tests/
```

`src/RestSharp` 是仓库内的结果类型工具项目，不应将其当成 HTTP 客户端库使用。

## 快速开始

### 环境要求

- Windows 开发环境。
- .NET 10 SDK。
- 支持 .NET 10 和 WPF 的 IDE，或使用 `dotnet` 命令行。
- 首次还原依赖时需要能够访问所使用的 NuGet 源。

### 还原、构建与运行

以下命令从**仓库根目录，也就是本 README 所在目录**开始执行：

```powershell
cd MachineApplication
dotnet restore .\MachineApplication.slnx
dotnet build .\MachineApplication.slnx -c Debug
dotnet run --project .\MachineApplication.Startup\MachineApplication.Startup.csproj
```

也可以在 IDE 中打开 `MachineApplication/MachineApplication.slnx`，将 `MachineApplication.Startup` 设为启动项目。

### 首次登录

权限数据库没有任何账号时，应用创建一个最高权限账号：

| 项目 | 初始值 |
| --- | --- |
| 用户名 | `xioa` |
| 密码 | `xioa` |

**首次使用后应立即修改默认密码，不要使用默认凭据部署真实业务。** 密码采用带随机盐的 PBKDF2 哈希存储；最高权限账号用于初始化权限等级及普通用户。

默认权限数据库位于 **exe 所在目录**下的 `setting/permissions.db`。已有数据库不会因为重启而重建账号；删除数据库会丢失账号、等级和按钮授权。

## 配置与数据位置

运行时配置以 **exe 所在目录**为基准，而不是终端当前工作目录。

| 文件或目录 | 用途 |
| --- | --- |
| `appSettings.json` | 日志后端、日志保留策略、启动页模式 |
| `Router.json` | 左侧菜单、标题、层级和页面访问等级 |
| `modules.xml` | 模块声明及加载信息 |
| `Theme.json` | 主题设置 |
| `setting/permissions.db` | 账号、权限等级、按钮定义与授权 |
| `Logs/` | 默认日志根目录，可通过配置修改 |

开发时，源配置位于 `MachineApplication.Startup`；应用内保存修改的是**运行目录中的配置文件**，不会自动回写源码目录。备份或部署时，应明确区分模板配置与现场配置。

应用需要对数据库、配置和日志所在目录具备写入权限。升级时应保留现场数据库及配置，不要直接用模板文件覆盖。

### 日志与启动配置

`appSettings.json` 示例：

```json
{
  "Logging": {
    "Provider": "NLog",
    "LogDirectory": "Logs",
    "MinimumLevel": "Trace",
    "FileSizeLimitBytes": 10485760,
    "RetainedFileCountLimit": 31,
    "RetentionDays": 30
  },
  "Startup": {
    "IndexPage": "Normal"
  }
}
```

| 配置项 | 说明 |
| --- | --- |
| `Logging.Provider` | `Serilog` 或 `NLog`，两个实现共用 `LoggingOptions` |
| `Logging.LogDirectory` | 日志根目录；相对路径基于 exe 目录解析 |
| `Logging.MinimumLevel` | `Trace`、`Debug`、`Info`、`Warn`、`Error`、`Fatal` |
| `Logging.FileSizeLimitBytes` | 单文件滚动大小上限，必须大于 0 |
| `Logging.RetainedFileCountLimit` | 每日目录内保留文件数限制，必须大于 0 |
| `Logging.RetentionDays` | 保留含今天在内的最近自然日数；`0` 关闭按天清理，不关闭文件数限制 |
| `Startup.IndexPage` | `Normal` 保留欢迎页；`Index` 在主窗口显示后打开菜单中第一个可访问页面 |

日志按日期分目录组织，例如 `Logs/2026-10-07/`，具体文件名由日志实现决定。

日志与启动配置修改后需要重启应用生效，不是热切换。`SuperPage` 提供配置编辑入口，保存前校验参数、备份为 `appSettings.json.bak`，通过 `JsonFileUtils` 写入，并保留不相关配置节。

`super` 路由已注册，但默认 `Router.json` 没有展示该菜单。需要时可通过路由配置添加入口，并明确设置访问等级。

## 页面与按钮权限

### 页面权限

页面权限使用 `page:路由` 标识，例如 `page:settings/users`。普通用户是否可访问，由 `Router.json` 中对应菜单项的 `RequiredLevelIds` 决定。

```json
{
  "LanguageKey": "MainWindow_Home",
  "Icon": "ViewDashboardOutline",
  "Url": "home",
  "RequiredLevelIds": [1, 2],
  "Titles": {
    "zh": "首页",
    "en": "Home"
  }
}
```

- `[1, 2]` 只是示例，需要使用实际创建的权限等级 ID。
- 最高权限账号始终可访问；普通账号需要匹配明确允许的等级。
- 未配置等级、等级列表为空或找不到对应路由配置时，普通账号不能访问。
- 左侧菜单隐藏无权页面，并移除没有可访问子页面的分组；导航入口仍执行权限检查。
- **路由注册与菜单配置是两件事**：注册了页面不代表它自动出现在菜单中。

### 按钮权限

在需要配置按钮权限的页面类上添加 `[BtnAuth]`，并为业务按钮提供稳定标识：

```csharp
using Machine.ModuleLoad.Mvvm;

[BtnAuth]
public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
}
```

```xml
<Button x:Name="SaveButton" Content="保存" Command="{Binding SaveCommand}" />
```

也可使用 `Machine.ModuleLoad.Mvvm.ButtonAuthorization.Id` 附加属性指定标识。按钮显示文案不参与权限键计算，因此切换语言不会改变授权；页面类型或按钮标识重命名后，应重新核对配置。

进入 **设置 → 按钮权限**，按“标记页面 → 页面按钮 → 可操作等级”配置并保存：

- 未启用按钮限制：不增加额外限制，沿用页面访问权限。
- 启用限制：仅勾选的等级可操作；不勾选任何等级表示仅管理员可操作。
- 保存到现有权限数据库，并通知已打开页面更新按钮可操作状态。
- 没有稳定标识的按钮只展示，不能保存授权。

目录查询针对当前已加载模块及检查实例中已初始化的按钮；未生成的模板、虚拟化项、`Loaded` 中才创建的按钮不在该目录内。模板内部按钮需要显式标识，不能依赖控件内部的 `PART_*` 名称。

**隐藏菜单或禁用按钮不能代替业务授权。** 敏感业务操作应在执行前调用 `PermissionService.Demand(...)`；按钮权限键可通过 `ButtonAuthorization.GetPermissionKey(pageType, buttonId)` 获取。本地 SQLite 权限方案也不等同于服务端鉴权。

## 主题与多语言

### 主题

主题页面支持明暗模式、主色及配色调整，设置保存在 `Theme.json`。新增页面优先使用项目已有的 `DynamicResource` 主题资源，避免写死背景和文字颜色。

### 增加语言

入口模块的资源位于：

```text
MachineApplication/src/MachineApplication.Entrance/Resources/
├─ EntranceLang.cs
├─ lang.zh.json
└─ lang.en.json
```

增加语言时：

1. 在同一资源目录新增 `lang.<语言标识>.json`，例如 `lang.ja.json`。
2. 保持键结构与已有资源一致，补齐相应翻译。
3. 重新构建，使源生成器生成资源访问代码。
4. 资源模块加载后，顶部语言菜单自动列出已注册语言，无需再修改中英文切换逻辑。

菜单显示语言自身名称及语言标识，并标记当前语言。当前实现从**已注册资源**获取可用语言，不会自动扫描运行目录中的任意 JSON 文件；仅向 exe 目录复制语言文件不会完成资源注册。

注意：菜单汇总各已加载模块的语言。如果只翻译部分模块，其他模块缺失的文案可能显示资源键，不会自动获得完整翻译。

## 日志与诊断

- 业务代码可使用 `GlobalLogger`，也可注入 `Machine.ModuleLoad.Logger.ILogger`；`AddLogger()` 负责容器注册及配置绑定。
- 只初始化所选的文件日志实现，避免同时写入两套相同日志。
- `DebuggerLogger` 可与调试输出、应用消息及控制台显示联动。
- `SuperPage` 支持外部 exe 启动后打开应用控制台，也可正常关闭该控制台。

自定义 `DbContext` 可在选项构建中接入 EF 日志：

```csharp
using Machine.ModuleLoad.Logger;

optionsBuilder.UseGlobalLogger();
```

权限数据库已接入此能力。记录包含 EF 事件、SQL 和执行耗时，执行失败也会记录。SQL 正常执行信息以 `Info` 输出，如需留存，应将最低日志等级设为 `Info` 或更低。默认不主动开启敏感参数值日志，避免密码等数据进入日志文件。

## 组件演示

`HomeView.xaml` 提供 Growl、LoadingBar 和 Snackbar 演示：

- **Growl**：信息、成功、警告、错误、严重错误通知，支持窗口内与桌面 Global 模式。
- **LoadingBar**：顶部任务加载状态及错误状态；通过 keyed DI 服务 `ILoadingBar`，键名 `LoadingBar` 获取。
- **Snackbar**：通过 `ISnackBar` 发送底部提示，支持消息排队。

导航遮挡用于页面切换反馈，**不表示 WPF 控件可以在后台线程创建**。页面中的数据库、文件或网络准备工作可通过 `IAsyncNavigationAware.PrepareAsync` 异步完成；控件创建和界面更新仍需在 UI 线程执行。避免在页面构造函数里执行长耗时业务。

## 扩展业务模块

建议从 `MachineApplication.Entrance/EntranceModule.cs` 和启动项目的 `modules.xml` 入手：

1. 实现 `IModule`，在 `RegisterTypes(IServiceCollection)` 中注册页面、ViewModel 和服务。
2. 在 `OnInitialized(IServiceProvider)` 中通过 `INavigationService.Register(...)` 注册路由。
3. 在 `modules.xml` 中声明模块，并确保相应程序集可被加载。
4. 页面通过 `[ModuleDataContext<TViewModel>("模块名")]` 使用框架的 DataContext 装配能力。
5. 在路由配置中添加菜单、标题及访问等级；需要按钮权限的页面再添加 `[BtnAuth]`。

当前入口模块使用 `Common` 单元模块，`modules.xml` 中配置 `LoadMode="SourceGenerator"`。模块配置、容器注册名称及页面使用的模块名应保持一致。

也可以从解决方案目录启动交互式生成工具：

```powershell
dotnet run --project .\MachineApplication.Create\MachineApplication.Create.csproj
```

工具按提示生成项目，可选生成模块及语言资源，并加入解决方案。生成完成后仍需核对应用的程序集引用、模块加载配置与业务路由。

## 发布

在 `MachineApplication` 解决方案目录执行：

```powershell
dotnet publish .\MachineApplication.Startup\MachineApplication.Startup.csproj `
  -c Release -r win-x64 --self-contained true -o .\artifacts\publish
```

发布后从输出目录启动 `MachineApplication.Startup.exe`。除程序集外，应检查 `appSettings.json`、`Router.json` 和 `modules.xml` 是否齐全；若 `modules.xml` 未进入输出目录，需要从启动项目复制。部署前检查模块引用路径、目录写入权限，并修改默认账号密码。

此示例采用 Windows x64 自包含发布；如选择依赖框架的发布方式，目标机器需安装相匹配的 .NET Desktop Runtime。项目未在此声明单文件、裁剪或 Native AOT 部署支持。

## 测试

仓库包含测试项目，以下命令供需要时手动执行：

```powershell
dotnet test .\tests\I18n.LangsGenerator.Tests\I18n.LangsGenerator.Tests.csproj
dotnet test .\tests\MachineApplication.Entrance.Tests\MachineApplication.Entrance.Tests.csproj
```

WPF 相关测试应在 Windows 环境中执行。测试项目存在不代表所有界面交互均已自动化覆盖；主题、动画、窗口操作等仍需结合实际运行检查。

## 许可证

本仓库采用 **Apache License 2.0**，具体条款见仓库根目录 `LICENSE`。第三方依赖遵循各自的许可证。
