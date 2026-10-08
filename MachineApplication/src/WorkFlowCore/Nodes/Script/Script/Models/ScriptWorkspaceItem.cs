namespace CsxPad.Wpf.Models;

public sealed record ScriptWorkspaceItem(
    string Name,
    string FullPath,
    bool IsFolder,
    IReadOnlyList<ScriptWorkspaceItem> Children,
    bool IsWorkspaceRoot = false);
