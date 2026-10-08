using CsxPad.Wpf.Services;

namespace CsxPad.Wpf.Scripting;

internal sealed record ConsoleScriptRequest(string Code, string? ScriptPath);

internal sealed record ConsoleScriptResultSection(
    string Title,
    ScriptResultLayout Layout,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

internal sealed record ConsoleScriptResponse(
    bool Success,
    int RowCount,
    string Output,
    IReadOnlyList<ConsoleScriptResultSection> ResultSections);
