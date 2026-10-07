# NLog 适配器

使用已有的 NLog 6.2.1，实现框架 `ILogger`。
统一配置参见上一级 `README.md`；在 `appSettings.json` 中设置 `Logging.Provider` 为 `NLog`。
其余配置全部来自同一个 `LoggingOptions`，原 `NLogLoggerOptions` 已移除。

```csharp
GlobalLogger.Info("业务消息");
GlobalLogger.NLogLogger?.Info("只写 NLog");
```

未选中时 `GlobalLogger.NLogLogger` 为 null。默认按本地日期写入程序目录的 `Logs/2026-10-07/log.log`，
跨午夜后自动切换目录，使用 UTF-8。达到配置的大小时在当天目录内归档，后缀包含序号；
归档保留数量仅作用于当天目录，不删除历史日期目录。
公共级别映射到 NLog 对应级别；`Success` 映射到 Info，并保留 `LevelName=SUCCESS`。
消息按普通文本处理，异常包含堆栈。

独立创建时同样使用公共选项：

```csharp
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Logger.Nlog;

using var logger = new NLogLogger(new LoggingOptions
{
    Provider = LoggingProvider.NLog,
    LogDirectory = "Logs/Custom",
    MinimumLevel = LoggingLevel.Info
});
logger.Info("独立实例");
```

每个自建实例拥有独立 `LogFactory`，不修改全局 `LogManager.Configuration` 或 `GlobalLogger`。
不同进程或独立实例应使用不同日志目录，不共享目标文件。
`new NLogLogger(existingLogger)` 默认不接管外部工厂的释放；
指定 `ownsFactory: true` 会在释放时关闭整个工厂，包括该工厂内其他 Logger。
