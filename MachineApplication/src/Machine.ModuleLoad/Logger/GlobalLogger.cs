using Machine.ModuleLoad.Logger.Debugger;

namespace Machine.ModuleLoad.Logger;

public static class GlobalLogger
{
    private static readonly ILogger[] Loggers;

    public static DebuggerLogger? DebuggerLogger { get; private set; }

    static GlobalLogger()
    {
        var loggers = new List<ILogger>();
        if (System.Diagnostics.Debugger.IsAttached)
        {
            DebuggerLogger = new DebuggerLogger();
            loggers.Add(DebuggerLogger);
        }

        Loggers = loggers.ToArray();
    }

    public static void Banner(string message)
    {
        // var debugger = Loggers.FirstOrDefault(e => e is DebuggerLogger);
        //
        // if (debugger == null) return;

        DebuggerLogger?.Banner(message);
    }

    /// <summary> 详细 </summary>
    public static void Trace(string message) =>
        Write(message, static (logger, text, _) => logger.Trace(text));

    /// <summary> 成功 </summary>
    public static void Success(string message) =>
        Write(message, static (logger, text, _) => logger.Success(text));

    /// <summary> 调试 </summary>
    public static void Debug(string message) =>
        Write(message, static (logger, text, _) => logger.Debug(text));

    /// <summary> 普通业务信息 </summary>
    public static void Info(string message) =>
        Write(message, static (logger, text, _) => logger.Info(text));

    /// <summary> 警告 </summary>
    public static void Warn(string message) =>
        Write(message, static (logger, text, _) => logger.Warn(text));

    /// <summary> 错误 </summary>
    public static void Error(string message, Exception? exception = null) =>
        Write(message, static (logger, text, error) => logger.Error(text, error), exception);

    /// <summary> 严重级别日志 </summary>
    public static void Fatal(string message, Exception? exception = null) =>
        Write(message, static (logger, text, error) => logger.Fatal(text, error), exception);

    private static void Write(
        string message,
        Action<ILogger, string, Exception?> write,
        Exception? exception = null)
    {
        foreach (var logger in Loggers)
        {
            write(logger, message, exception);
        }
    }
}