using System.Globalization;
using System.IO;
using System.Text;
using global::Serilog;
using global::Serilog.Core;
using global::Serilog.Events;

namespace Machine.ModuleLoad.Logger.Serilog;

internal sealed class DailyDirectorySink : ILogEventSink, IDisposable
{
    private readonly string _logDirectory;
    private readonly long _fileSizeLimitBytes;
    private readonly int _retainedFileCountLimit;
    private readonly object _syncRoot = new();
    private global::Serilog.Core.Logger? _logger;
    private DateOnly _date;
    private bool _disposed;

    public DailyDirectorySink(LoggingOptions options)
    {
        _logDirectory = options.ResolveLogDirectory();
        _fileSizeLimitBytes = options.FileSizeLimitBytes;
        _retainedFileCountLimit = options.RetainedFileCountLimit;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        lock (_syncRoot)
        {
            if (_disposed) return;
            var date = DateOnly.FromDateTime(logEvent.Timestamp.LocalDateTime);
            if (_logger is null || _date != date)
            {
                var directory = Path.Combine(_logDirectory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                var nextLogger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(
                        path: Path.Combine(directory, "log.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{LevelName,-7}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture,
                        fileSizeLimitBytes: _fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: _retainedFileCountLimit,
                        buffered: false,
                        shared: true,
                        encoding: new UTF8Encoding(false))
                    .CreateLogger();
                var previousLogger = _logger;
                _logger = nextLogger;
                _date = date;
                previousLogger?.Dispose();
            }
            _logger.Write(logEvent);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            _logger?.Dispose();
            _logger = null;
        }
    }
}
