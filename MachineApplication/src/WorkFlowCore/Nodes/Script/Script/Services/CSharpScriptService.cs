using CSharpScriptCore.Models;
using CsxPad.Wpf.Scripting;
using CoreScript = CSharpScriptCore.CSharpScript;

namespace CsxPad.Wpf.Services;

/// <summary>
/// 将 CSharpScriptCore 的通用执行结果适配为 WPF 界面使用的数据视图。
/// </summary>
public sealed class CSharpScriptService(bool isolateConsoleScripts = true, string? packageWorkspaceDirectory = null)
{
    public Task<ScriptRunResult> RunAsync(string code, CancellationToken cancellationToken) =>
        RunCoreAsync(code, null, cancellationToken, null);

    public Task<ScriptRunResult> RunAsync(
        string code,
        string? scriptPath,
        CancellationToken cancellationToken) =>
        RunCoreAsync(code, null, cancellationToken, scriptPath);

    public Task<ScriptRunResult> DebugAsync(
        string code,
        ScriptDebugSession debugSession,
        CancellationToken cancellationToken) =>
        RunCoreAsync(code, debugSession, cancellationToken, null);

    public Task<ScriptRunResult> DebugAsync(
        string code,
        string? scriptPath,
        ScriptDebugSession debugSession,
        CancellationToken cancellationToken) =>
        RunCoreAsync(code, debugSession, cancellationToken, scriptPath);

    private async Task<ScriptRunResult> RunCoreAsync(
        string code,
        ScriptDebugSession? debugSession,
        CancellationToken cancellationToken,
        string? scriptPath)
    {
        if (CoreScript.UsesFramework(code, "Console") && isolateConsoleScripts)
        {
            return await ConsoleScriptProcessRunner.RunAsync(code, scriptPath, cancellationToken, packageWorkspaceDirectory);
        }

        using var debugScope = debugSession is null ? null : ScriptDebugger.BeginSession(debugSession);
        var executableCode = debugSession is null
            ? code
            : ScriptDebugInstrumenter.Instrument(code, debugSession.Breakpoints);
        var options = CoreScript.CreateOptions(scriptPath);
        if (!string.IsNullOrWhiteSpace(packageWorkspaceDirectory))
            options.PackageWorkspaceDirectory = packageWorkspaceDirectory;
        options.References.Add(typeof(ScriptDebugger).Assembly.Location);
        var result = await CoreScript.ExecuteCodeAsync(executableCode, options, cancellationToken);
        if (result.RunResult is ScriptRunStatus.Cancelled or ScriptRunStatus.Timeout &&
            cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var tables = ScriptResultTable.CreateAll(result);
        var sections = tables
            .Select(table => new ScriptResultSection(
                table.Title,
                table.View,
                table.RowCount,
                table.Layout))
            .ToArray();
        var primarySection = sections.LastOrDefault() ?? ScriptResultSection.Empty;
        var totalRowCount = sections.Sum(section => section.RowCount);
        var dumpSummary = result.Success || result.Dumps.Count > 0
            ? CreateDumpSummary(result.Dumps)
            : string.Empty;
        var output = string.Join(
            Environment.NewLine,
            new[] { result.Output, dumpSummary }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new ScriptRunResult(
            result.Success,
            primarySection.View,
            totalRowCount,
            output,
            primarySection.Layout,
            sections);
    }

    private static string CreateDumpSummary(IReadOnlyList<ScriptDump> dumps)
    {
        if (dumps.Count == 0)
        {
            return "Script completed without Dump() output.";
        }

        return string.Join(Environment.NewLine, dumps.Select((dump, index) =>
        {
            var count = ScriptResultTable.Materialize(dump.Value).Count;
            var name = string.IsNullOrWhiteSpace(dump.Title) ? $"Dump {index + 1}" : dump.Title;
            return $"{name}: {count} value(s)";
        }));
    }
}

public enum ScriptResultLayout
{
    Empty,
    Scalar,
    ScalarSequence,
    Object,
    ObjectSequence
}

public sealed record ScriptResultSection(
    string Title,
    System.Data.DataView View,
    int RowCount,
    ScriptResultLayout Layout)
{
    public bool ShowTitle => !string.IsNullOrWhiteSpace(Title);

    public bool ShowHeaders => Layout is ScriptResultLayout.ScalarSequence or ScriptResultLayout.ObjectSequence;

    public static ScriptResultSection Empty { get; } = CreateEmpty();

    private static ScriptResultSection CreateEmpty()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("Result", typeof(object));
        return new ScriptResultSection(string.Empty, table.DefaultView, 0, ScriptResultLayout.Empty);
    }
}

public sealed record ScriptRunResult(
    bool Success,
    System.Data.DataView ResultView,
    int RowCount,
    string Output,
    ScriptResultLayout Layout,
    IReadOnlyList<ScriptResultSection> ResultSections)
{
    public static ScriptRunResult Failed(string output)
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("Result", typeof(object));
        return new ScriptRunResult(
            false,
            table.DefaultView,
            0,
            output,
            ScriptResultLayout.Empty,
            []);
    }
}
