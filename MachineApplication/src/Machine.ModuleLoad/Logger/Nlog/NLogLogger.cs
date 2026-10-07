using System.Globalization;
using System.IO;
using System.Text;
using global::NLog;
using global::NLog.Config;
using global::NLog.Layouts;
using global::NLog.Targets;

namespace Machine.ModuleLoad.Logger.Nlog;

public sealed class NLogLogger : global::Machine.ModuleLoad.Logger.ILogger, IDisposable
{
    private readonly global::NLog.Logger _logger;
    private readonly bool _ownsFactory;
    private readonly DailyLogRetention? _retention;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public NLogLogger(LoggingOptions? options = null)
    {
        options ??= new LoggingOptions();
        options.Validate();
        var minimumLevel = options.MinimumLevel switch
        {
            LoggingLevel.Trace => LogLevel.Trace,
            LoggingLevel.Debug => LogLevel.Debug,
            LoggingLevel.Info => LogLevel.Info,
            LoggingLevel.Warn => LogLevel.Warn,
            LoggingLevel.Error => LogLevel.Error,
            LoggingLevel.Fatal => LogLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(options.MinimumLevel))
        };

        var logDirectory = options.ResolveLogDirectory();
        var factory = new LogFactory();
        try
        {
            var configuration = new LoggingConfiguration(factory)
            {
                DefaultCultureInfo = CultureInfo.InvariantCulture
            };
            var target = new FileTarget("file")
            {
                FileName = new SimpleLayout(Path.Combine(SimpleLayout.Escape(logDirectory), "${shortdate}", "log.log")),
                Layout = "[${longdate}] [${event-properties:item=LevelName:padding=-7}] ${message}${onexception:inner=${newline}${exception:format=tostring}}",
                Encoding = new UTF8Encoding(false),
                ArchiveAboveSize = options.FileSizeLimitBytes,
                ArchiveSuffixFormat = "_{0:00}",
                MaxArchiveFiles = options.RetainedFileCountLimit,
                KeepFileOpen = true,
                AutoFlush = true
            };
            configuration.AddRule(minimumLevel, LogLevel.Fatal, target);
            factory.Configuration = configuration;
            _logger = factory.GetLogger("Machine.ModuleLoad");
            _ownsFactory = true;
            _retention = new DailyLogRetention(options);
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }

    /// <summary>适配已有 NLog 实例；默认由原持有方负责释放其工厂。</summary>
    public NLogLogger(global::NLog.Logger logger, bool ownsFactory = false)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _ownsFactory = ownsFactory;
    }

    public void Trace(string message) => Write(LogLevel.Trace, "TRACE", message);

    public void Debug(string message) => Write(LogLevel.Debug, "DEBUG", message);

    public void Info(string message) => Write(LogLevel.Info, "INFO", message);

    public void Success(string message) => Write(LogLevel.Info, "SUCCESS", message);

    public void Warn(string message) => Write(LogLevel.Warn, "WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write(LogLevel.Error, "ERROR", message, exception);

    public void Fatal(string message, Exception? exception = null) =>
        Write(LogLevel.Fatal, "FATAL", message, exception);

    private void Write(LogLevel level, string levelName, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_syncRoot)
        {
            if (_disposed || !_logger.IsEnabled(level)) return;
            var logEvent = new LogEventInfo(level, _logger.Name, message)
            {
                Exception = exception,
                MessageFormatter = static entry => entry.Message
            };
            logEvent.Properties["LevelName"] = levelName;
            _logger.Log(logEvent);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            _retention?.Dispose();
            if (!_ownsFactory) return;
            try
            {
                _logger.Factory.Flush(TimeSpan.FromSeconds(5));
            }
            finally
            {
                _logger.Factory.Dispose();
            }
        }
    }
}
