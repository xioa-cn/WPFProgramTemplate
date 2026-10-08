using System.IO;

namespace CsxPad.Wpf.Services;

public static class WorkspaceItemPathResolver
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static WorkspacePathResolution Resolve(
        string targetDirectory,
        string input,
        bool isFolder,
        string? existingPath = null)
    {
        var relativePath = input.Trim();
        if (relativePath.Length == 0)
        {
            return WorkspacePathResolution.Failed(isFolder ? "Enter a folder name." : "Enter a file name.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            return WorkspacePathResolution.Failed("Use a path relative to the selected folder.");
        }

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return WorkspacePathResolution.Failed("The path cannot contain '.' or '..' segments.");
        }

        if (segments.Any(IsInvalidSegment))
        {
            return WorkspacePathResolution.Failed("The path contains reserved characters or names.");
        }

        if (!isFolder && !segments[^1].EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] += ".csx";
        }

        var root = Path.GetFullPath(targetDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePathResolution.Failed("The path must stay inside the selected folder.");
        }

        var resolvesToExistingItem = !string.IsNullOrWhiteSpace(existingPath) &&
                                     string.Equals(
                                         candidate,
                                         Path.GetFullPath(existingPath),
                                         StringComparison.OrdinalIgnoreCase);
        if (!resolvesToExistingItem && (File.Exists(candidate) || Directory.Exists(candidate)))
        {
            return WorkspacePathResolution.Failed(
                $"{string.Join(Path.DirectorySeparatorChar, segments)} already exists.");
        }

        return WorkspacePathResolution.Succeeded(candidate);
    }

    private static bool IsInvalidSegment(string segment)
    {
        if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            segment.EndsWith(' ') || segment.EndsWith('.'))
        {
            return true;
        }

        var deviceName = Path.GetFileNameWithoutExtension(segment);
        return ReservedNames.Contains(deviceName);
    }
}

public sealed record WorkspacePathResolution(string? FullPath, string? Error)
{
    public bool IsValid => FullPath is not null && Error is null;

    public static WorkspacePathResolution Succeeded(string fullPath) => new(fullPath, null);

    public static WorkspacePathResolution Failed(string error) => new(null, error);
}
