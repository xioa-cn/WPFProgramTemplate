using Machine.ModuleLoad.Logger.Debugger;

using Machine.ModuleLoad.Logger.Serilog;
using Machine.ModuleLoad.Logger.Nlog;

namespace Machine.ModuleLoad.Logger;

public static class GlobalLogger
{
    private static readonly object SyncRoot = new();
    private static ILogger[] _loggers = [];
    private static bool _closed;

    public static DebuggerLogger? DebuggerLogger { get; private set; }

    public static SerilogLogger? SerilogLogger { get; private set; }

    public static NLogLogger? NLogLogger { get; private set; }

    public static string? Provider { get; private set; }

    static GlobalLogger()
    {
        if (System.Diagnostics.Debugger.IsAttached)
        {
            DebuggerLogger = new DebuggerLogger();
            _loggers = [DebuggerLogger];
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => CloseAndFlush();
    }

    internal static void EnableDebuggerLogger()
    {
        lock (SyncRoot)
        {
            if (_closed) throw new InvalidOperationException("Global logging has been closed.");
            DebuggerLogger ??= new DebuggerLogger();
            if (!_loggers.Contains(DebuggerLogger))
                Volatile.Write(ref _loggers, [.. _loggers, DebuggerLogger]);
        }
    }

    internal static void Initialize(LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var provider = options.Provider.ToString();

        lock (SyncRoot)
        {
            if (_closed) throw new InvalidOperationException("全局日志已关闭，无法重新初始化。");
            if (Provider is not null)
            {
                if (Provider != provider)
                    throw new InvalidOperationException("全局日志已初始化，切换日志库需要重启应用。");
                return;
            }

            var loggers = new List<ILogger>();
            if (provider == "NLog")
            {
                NLogLogger = new NLogLogger(options);
                loggers.Add(NLogLogger);
            }
            else
            {
                SerilogLogger = new SerilogLogger(options);
                loggers.Add(SerilogLogger);
            }

            if (DebuggerLogger is not null) loggers.Add(DebuggerLogger);
            Provider = provider;
            Volatile.Write(ref _loggers, loggers.ToArray());
        }
    }

    /// <summary>释放并刷新内置文件日志；重复调用安全，关闭后不再写入。</summary>
    public static void CloseAndFlush()
    {
        lock (SyncRoot)
        {
            if (_closed) return;
            _closed = true;
            try
            {
                SerilogLogger?.Dispose();
            }
            finally
            {
                NLogLogger?.Dispose();
            }
        }
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
        foreach (var logger in Volatile.Read(ref _loggers))
        {
            write(logger, message, exception);
        }
    }
}
