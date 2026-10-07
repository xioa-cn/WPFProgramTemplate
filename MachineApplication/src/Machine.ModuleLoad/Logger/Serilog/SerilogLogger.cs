using global::Serilog;
using global::Serilog.Events;

namespace Machine.ModuleLoad.Logger.Serilog;

public sealed class SerilogLogger : global::Machine.ModuleLoad.Logger.ILogger, IDisposable
{
    private readonly global::Serilog.ILogger _logger;
    private readonly bool _ownsLogger;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public SerilogLogger(LoggingOptions? options = null)
    {
        options ??= new LoggingOptions();
        options.Validate();
        var minimumLevel = options.MinimumLevel switch
        {
            LoggingLevel.Trace => LogEventLevel.Verbose,
            LoggingLevel.Debug => LogEventLevel.Debug,
            LoggingLevel.Info => LogEventLevel.Information,
            LoggingLevel.Warn => LogEventLevel.Warning,
            LoggingLevel.Error => LogEventLevel.Error,
            LoggingLevel.Fatal => LogEventLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(options.MinimumLevel))
        };

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.Sink(new DailyDirectorySink(options))
            .CreateLogger();
        _ownsLogger = true;
    }

    /// <summary>适配已有 Serilog 实例；默认不接管该实例的释放。</summary>
    public SerilogLogger(global::Serilog.ILogger logger, bool ownsLogger = false)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _ownsLogger = ownsLogger;
    }

    public void Trace(string message) => Write(LogEventLevel.Verbose, "TRACE", message);

    public void Debug(string message) => Write(LogEventLevel.Debug, "DEBUG", message);

    public void Info(string message) => Write(LogEventLevel.Information, "INFO", message);

    public void Success(string message) => Write(LogEventLevel.Information, "SUCCESS", message);

    public void Warn(string message) => Write(LogEventLevel.Warning, "WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write(LogEventLevel.Error, "ERROR", message, exception);

    public void Fatal(string message, Exception? exception = null) =>
        Write(LogEventLevel.Fatal, "FATAL", message, exception);

    private void Write(LogEventLevel level, string levelName, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_syncRoot)
        {
            if (_disposed || !_logger.IsEnabled(level)) return;
            _logger.ForContext("LevelName", levelName).Write(level, exception, "{Text:l}", message);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsLogger && _logger is IDisposable disposable) disposable.Dispose();
        }
    }
}
