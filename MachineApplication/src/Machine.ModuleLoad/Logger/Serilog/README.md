# Serilog 适配器

使用 Serilog 4.4.0 与 Serilog.Sinks.File 7.0.0，实现框架 `ILogger`。
统一配置参见上一级 `README.md`；在 `appSettings.json` 中设置 `Logging.Provider` 为 `Serilog`。
其余配置全部来自同一个 `LoggingOptions`，原 `SerilogLoggerOptions` 已移除。

```csharp
GlobalLogger.Info("业务消息");
GlobalLogger.SerilogLogger?.Info("只写 Serilog");
```

未选中时 `GlobalLogger.SerilogLogger` 为 null。默认写入程序目录的 `Logs`，
按本地日期分目录，如 `Logs/2026-10-07/log.log`，跨午夜后自动切换目录，使用 UTF-8。
达到配置的大小时在当天目录内滚动，文件保留数量也仅作用于当天目录，不删除历史日期目录。
`Trace` 映射到 Verbose，`Info`/`Success` 映射到 Information，`Warn` 映射到 Warning。
`LevelName` 保留 SUCCESS 等框架级别标识，消息按普通文本处理，异常包含堆栈。

独立创建时同样使用公共选项：

```csharp
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Logger.Serilog;

using var logger = new SerilogLogger(new LoggingOptions
{
    Provider = LoggingProvider.Serilog,
    LogDirectory = "Logs/Custom",
    MinimumLevel = LoggingLevel.Info
});
logger.Info("独立实例");
```

独立实例不修改 `GlobalLogger`。`new SerilogLogger(existingLogger)` 默认不接管外部实例的释放；
显式指定 `ownsLogger: true` 才由适配器释放。不替换 `Serilog.Log.Logger`。
