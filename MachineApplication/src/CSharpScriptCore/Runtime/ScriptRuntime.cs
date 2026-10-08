using CSharpScriptCore.Models;

namespace CSharpScriptCore.Runtime;

/// <summary>
/// 提供给 CSX 脚本使用的运行时辅助方法。
/// </summary>
public static class ScriptRuntime
{
    private static readonly AsyncLocal<ExecutionContext?> CurrentContext = new();

    /// <summary>当前脚本的取消令牌。</summary>
    public static CancellationToken CancellationToken =>
        CurrentContext.Value?.CancellationToken ?? System.Threading.CancellationToken.None;

    /// <summary>把值发送到宿主结果面板，并原样返回该值。</summary>
    public static T Dump<T>(this T value, string? title = null)
    {
        CurrentContext.Value?.Dumps.Add(new ScriptDump(value, title));
        return value;
    }

    internal static IDisposable Begin(CancellationToken cancellationToken, List<ScriptDump> dumps)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = new ExecutionContext(cancellationToken, dumps);
        return new Scope(() => CurrentContext.Value = previous);
    }

    private sealed record ExecutionContext(CancellationToken CancellationToken, List<ScriptDump> Dumps);

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
