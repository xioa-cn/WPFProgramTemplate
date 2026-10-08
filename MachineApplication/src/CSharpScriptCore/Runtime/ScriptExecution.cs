namespace CSharpScriptCore.Runtime;

/// <summary>
/// 提供当前 CSX 脚本的执行上下文。
/// </summary>
/// <remarks>
/// 此类型保留了已有脚本使用的公共入口。新代码也可以直接使用
/// <see cref="ScriptRuntime"/> 提供的运行时功能。
/// </remarks>
public static class ScriptExecution
{
    /// <summary>
    /// 获取当前脚本的取消令牌；脚本不在执行上下文中时返回不可取消的令牌。
    /// </summary>
    public static CancellationToken CancellationToken => ScriptRuntime.CancellationToken;
}
