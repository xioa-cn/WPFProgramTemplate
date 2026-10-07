# 日志配置

Serilog 和 NLog 共用 `LoggingOptions`，不再提供各自的 Options 类型。
启动项目的 `appSettings.json` 使用同一个配置节：

```json
{
  "Logging": {
    "Provider": "Serilog",
    "LogDirectory": "Logs",
    "MinimumLevel": "Trace",
    "FileSizeLimitBytes": 10485760,
    "RetainedFileCountLimit": 31
  }
}
```

- `Provider`：`Serilog` 或 `NLog`，只初始化所选实现。
- `LogDirectory`：支持绝对路径；相对路径基于 `AppContext.BaseDirectory`，默认是程序目录下的 `Logs`。两套实现均按日志的本地日期写入 `yyyy-MM-dd` 子目录，例如 `Logs/2026-10-07/log.log`，跨午夜后自动写入新日期目录。部署到只读目录时请配置可写的绝对路径。
- `MinimumLevel`：`Trace`、`Debug`、`Info`、`Warn`、`Error`、`Fatal`，两套适配器自行映射到底层级别。`Success` 按 `Info` 级别过滤。
- `FileSizeLimitBytes`：单文件滚动阈值，单位字节，必须大于零。
- `RetainedFileCountLimit`：必须大于零，仅限制每个日期目录内的文件数量，不是保留天数，也不会删除历史日期目录。Serilog 包含当前文件，NLog 按归档文件数限制，另有一个当前文件。

旧版本生成的日志保留原位，不自动迁移或清理。独立同时使用两套日志实现时，应配置不同的日志根目录，避免写入同一个文件。

默认值与示例一致。配置使用 `AddJsonFile` 加载，再通过
`AddOptions<LoggingOptions>().BindConfiguration("Logging")` 绑定，启动时解析并验证 `IOptions<LoggingOptions>.Value`。
配置文件自动复制到输出和发布目录；修改后重启生效，不进行热切换。
缺失项使用默认值，非法枚举、空目录、非正数或损坏的 JSON 会使启动报错。

主容器和模块子容器共享配置，可通过构造函数注入 `IOptions<LoggingOptions>`，不需要自行解析 JSON。
Options 是启动配置，不应在运行中直接修改其属性来切换日志。

`services.AddLogger()` 注册框架的 `Machine.ModuleLoad.Logger.ILogger` 单例，同时加载上述配置和 Options 验证。
WPF 的 `BuildWpfServiceProvider()` 自动调用此方法，模块子容器自动共享主容器的 `ILogger`。
重复调用不会重复注册，也不覆盖已注册的 `ILogger` 或 `IConfiguration`。
构造函数直接注入 `ILogger` 即可，默认实现转发到 `GlobalLogger`，复用已选文件日志和调试消息通知，不创建第二套文件日志。
日志仍由 `GlobalLogger.CloseAndFlush()` 或进程退出时统一释放，释放模块容器不会关闭全局日志。

```csharp
public sealed class SaveService(ILogger logger)
{
    public void Save() => logger.Info("保存业务数据");
}
```

```csharp
GlobalLogger.Info("业务消息");
GlobalLogger.Success("保存成功");
GlobalLogger.Error("保存失败", exception);
```

以上方法写入所选文件日志和已启用的调试日志；直接调用 `DebuggerLogger` 仍只输出调试日志。
`GlobalLogger.Provider` 显示当前提供程序；初始化前为 null。正常进程退出时自动刷新并释放，
`GlobalLogger.CloseAndFlush()` 可以主动终止记录，调用后不能重新初始化。

配置页提供即时打开、关闭调试控制台的操作，也可调用 `CmdTools.OpenConsole()` / `CmdTools.CloseConsole()`。
外部 EXE 启动时手动打开会启用 `GlobalLogger.DebuggerLogger`，同时接收全局日志与注入的 `ILogger` 输出；不回放打开前的消息。
关闭仅释放应用创建的控制台，恢复之前的标准输入输出，不停止文件日志或退出 WPF 应用；可再次打开。
原生控制台关闭菜单禁用，请通过页面按钮关闭，避免 Windows 控制台关闭行为终止宿主程序。
IDE 已重定向的输出保持原样；手动打开应用控制台期间输出到该控制台，关闭后恢复 IDE 输出。不接管已有的外部终端。
