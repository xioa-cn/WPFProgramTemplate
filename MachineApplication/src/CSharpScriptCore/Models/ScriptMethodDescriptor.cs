namespace CSharpScriptCore.Models;

/// <summary>可调用脚本方法的签名描述。</summary>
public sealed record ScriptMethodDescriptor(
    string Name,
    string ReturnType,
    bool IsStatic,
    bool IsGeneric,
    IReadOnlyList<ScriptParameterDescriptor> Parameters);

/// <summary>脚本方法参数描述。</summary>
public sealed record ScriptParameterDescriptor(
    string Name,
    string Type,
    bool IsOptional,
    bool IsParams,
    bool IsOut,
    bool IsByRef);
