namespace CSharpScriptCore.Models;

/// <summary>
/// CSX 脚本执行结果。
/// </summary>
public class ScriptResult
{
    /// <summary>脚本最终状态。</summary>
    public ScriptRunStatus RunResult { get; init; } = ScriptRunStatus.Idle;

    /// <summary>编译或运行过程中产生的异常；成功时为 <see langword="null"/>。</summary>
    public Exception? Exception { get; init; }

    /// <summary>从准备依赖到执行结束所消耗的毫秒数。</summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>Roslyn 编译诊断文本。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    /// <summary>包含文件和行列信息的结构化编译诊断。</summary>
    public IReadOnlyList<ScriptDiagnostic> StructuredDiagnostics { get; init; } = [];

    /// <summary>依赖还原、编译或运行阶段产生的宿主输出。</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>脚本最后一个表达式的原始返回值。</summary>
    public object? ReturnValue { get; init; }

    /// <summary>脚本通过 <c>Dump()</c> 输出的值。</summary>
    public IReadOnlyList<ScriptDump> Dumps { get; init; } = [];

    /// <summary>依赖准备、编译和执行阶段的耗时。</summary>
    public ScriptExecutionTimings? Timings { get; init; }

    /// <summary>脚本是否成功执行完成。</summary>
    public bool Success => RunResult == ScriptRunStatus.Success;
}

/// <summary>
/// 带强类型返回值的 CSX 脚本执行结果。
/// </summary>
/// <typeparam name="T">脚本返回值类型。</typeparam>
public class ScriptResult<T> : ScriptResult
{
    /// <summary>转换后的脚本返回值。</summary>
    public T? Data { get; init; }
}

/// <summary>
/// 脚本类实例的创建结果。成功时 <see cref="ScriptResult{T}.Data"/> 保存可重复调用的实例。
/// </summary>
public sealed class ScriptClass<T> : ScriptResult<T>, IDisposable, IAsyncDisposable
{
    private int _disposed;

    /// <summary>释放脚本实例持有的同步资源。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && Data is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>优先异步释放脚本实例；不存在异步释放能力时退回同步释放。</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (Data is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (Data is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// 一次 <c>Dump()</c> 调用产生的数据。
/// </summary>
/// <param name="Value">输出值。</param>
/// <param name="Title">可选标题。</param>
public sealed record ScriptDump(object? Value, string? Title);
