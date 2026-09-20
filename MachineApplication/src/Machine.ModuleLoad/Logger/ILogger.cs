namespace Machine.ModuleLoad.Logger;

public interface ILogger
{
    /// <summary> 详细 </summary>
    void Trace(string message);

    /// <summary> 调试 </summary>
    void Debug(string message);

    /// <summary> 普通业务信息 </summary>
    void Info(string message);
    
    void Success(string message);

    /// <summary> 警告 </summary>
    void Warn(string message);

    /// <summary> 错误 </summary>
    void Error(string message, Exception? exception = null);

    /// <summary> 严重级别日志 </summary>
    void Fatal(string message, Exception? exception = null);
}