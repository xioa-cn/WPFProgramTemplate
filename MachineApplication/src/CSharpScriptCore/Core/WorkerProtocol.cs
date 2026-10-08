using System.Text.Json;
using CSharpScriptCore.Models;

namespace CSharpScriptCore.Core;

internal enum WorkerOperation
{
    ExecuteCode,
    ExecuteFile,
    RunStaticCode,
    RunStaticFile
}

internal sealed record WorkerValue(string? TypeName, string Json)
{
    public static WorkerValue FromObject(object? value)
    {
        if (value is null)
        {
            return new WorkerValue(null, "null");
        }

        var type = value.GetType();
        return new WorkerValue(type.AssemblyQualifiedName, JsonSerializer.Serialize(value, type, WorkerJson.Options));
    }

    public object? Deserialize(Type targetType) =>
        JsonSerializer.Deserialize(Json, targetType, WorkerJson.Options);
}

internal sealed record WorkerExecutionOptions(
    string? ScriptPath,
    string BaseDirectory,
    string PackageWorkspaceDirectory,
    string[] References,
    string[] Imports,
    bool IncludeLoadedAssemblies,
    bool EnableNuGetDirectives,
    string[] NuGetSources,
    string? NuGetConfigFile,
    string? RuntimeIdentifier,
    bool EnableCompilationCache)
{
    public static WorkerExecutionOptions FromOptions(ScriptExecutionOptions options) => new(
        options.ScriptPath,
        options.BaseDirectory,
        options.PackageWorkspaceDirectory,
        options.References.ToArray(),
        options.Imports.ToArray(),
        options.IncludeLoadedAssemblies,
        options.EnableNuGetDirectives,
        options.NuGetSources.ToArray(),
        options.NuGetConfigFile,
        options.RuntimeIdentifier,
        options.EnableCompilationCache);

    public ScriptExecutionOptions ToOptions()
    {
        var options = new ScriptExecutionOptions
        {
            ScriptPath = ScriptPath,
            BaseDirectory = BaseDirectory,
            PackageWorkspaceDirectory = PackageWorkspaceDirectory,
            IncludeLoadedAssemblies = IncludeLoadedAssemblies,
            EnableNuGetDirectives = EnableNuGetDirectives,
            NuGetConfigFile = NuGetConfigFile,
            RuntimeIdentifier = RuntimeIdentifier,
            EnableCompilationCache = EnableCompilationCache
        };
        foreach (var reference in References) options.References.Add(reference);
        foreach (var import in Imports) options.Imports.Add(import);
        foreach (var source in NuGetSources) options.NuGetSources.Add(source);
        return options;
    }
}

internal sealed record WorkerRequest(
    WorkerOperation Operation,
    string Source,
    string? ClassName,
    string? MethodName,
    WorkerValue[] Parameters,
    WorkerExecutionOptions Options);

internal sealed record WorkerResponse(
    ScriptRunStatus RunResult,
    string? ExceptionType,
    string? ExceptionMessage,
    long ElapsedMilliseconds,
    string[] Diagnostics,
    ScriptDiagnostic[] StructuredDiagnostics,
    string Output,
    WorkerValue? ReturnValue,
    ScriptExecutionTimings? Timings);

internal static class WorkerJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
