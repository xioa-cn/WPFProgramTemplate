using Microsoft.Extensions.Options;

namespace Machine.ModuleLoad.Logger;

internal sealed class GlobalLoggerAdapter : ILogger
{
    public GlobalLoggerAdapter(IOptions<LoggingOptions> options)
    {
        GlobalLogger.Initialize(options.Value);
    }

    public void Trace(string message) => GlobalLogger.Trace(message);

    public void Debug(string message) => GlobalLogger.Debug(message);

    public void Info(string message) => GlobalLogger.Info(message);

    public void Success(string message) => GlobalLogger.Success(message);

    public void Warn(string message) => GlobalLogger.Warn(message);

    public void Error(string message, Exception? exception = null) => GlobalLogger.Error(message, exception);

    public void Fatal(string message, Exception? exception = null) => GlobalLogger.Fatal(message, exception);
}
