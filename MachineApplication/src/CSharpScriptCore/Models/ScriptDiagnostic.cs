namespace CSharpScriptCore.Models;

/// <summary>结构化的脚本编译诊断。</summary>
public sealed record ScriptDiagnostic(
    string Id,
    string Severity,
    string Message,
    string? FilePath,
    int? StartLine,
    int? StartColumn,
    int? EndLine,
    int? EndColumn);

/// <summary>脚本各执行阶段的耗时。</summary>
public sealed record ScriptExecutionTimings(
    long DependencyMilliseconds,
    long CompilationMilliseconds,
    long ExecutionMilliseconds,
    long TotalMilliseconds);
