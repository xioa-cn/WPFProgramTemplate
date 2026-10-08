using System.IO;
using System.Text.Json;

namespace CsxPad.Wpf.Services;

public sealed class WorkspaceSessionService
{
    private readonly string _statePath;

    public WorkspaceSessionService(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CsxPad",
            "workspace-session.json");
    }

    public WorkspaceSessionState Load()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return WorkspaceSessionState.Empty;
            }

            return JsonSerializer.Deserialize<WorkspaceSessionState>(File.ReadAllText(_statePath))
                   ?? WorkspaceSessionState.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return WorkspaceSessionState.Empty;
        }
    }

    public void Save(string? workspacePath, string? documentPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _statePath,
                JsonSerializer.Serialize(new WorkspaceSessionState(workspacePath, documentPath)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Session persistence must never prevent the editor from opening or switching files.
        }
    }
}

public sealed record WorkspaceSessionState(string? WorkspacePath, string? DocumentPath)
{
    public static WorkspaceSessionState Empty { get; } = new(null, null);
}
