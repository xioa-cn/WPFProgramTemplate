namespace CSharpScriptCore.Models;

/// <summary>独立 Worker 进程的启动、超时和 Console 配置。</summary>
public sealed class ScriptWorkerOptions
{
    /// <summary>Worker 可执行文件或 DLL 路径。为空时从应用目录自动查找。</summary>
    public string? WorkerPath { get; set; }

    /// <summary>硬超时。到期后终止整个 Worker 进程树。默认 30 秒。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>一次性写入 Worker 标准输入的文本；写入后关闭输入流。</summary>
    public string ConsoleInput { get; set; } = string.Empty;

    /// <summary>Worker 标准输出完成后的回调。</summary>
    public Action<string>? StandardOutputReceived { get; set; }

    /// <summary>Worker 标准错误完成后的回调。</summary>
    public Action<string>? StandardErrorReceived { get; set; }

    /// <summary>传递给 Worker 的附加环境变量。</summary>
    public IDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>脚本编译和依赖选项。Globals 不支持跨进程传输。</summary>
    public ScriptExecutionOptions ExecutionOptions { get; set; } = new();
}
