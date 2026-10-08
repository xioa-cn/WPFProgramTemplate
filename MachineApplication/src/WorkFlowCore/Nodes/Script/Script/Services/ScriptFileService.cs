using System.IO;
using System.Windows;
using CsxPad.Wpf.Dialogs;
using Microsoft.Win32;

namespace CsxPad.Wpf.Services;

public sealed class ScriptFileService
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".idea", ".vs", "bin", "obj"
    };

    public ScriptDocument? Open(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open C# script",
            Filter = "C# Script (*.csx)|*.csx|All files (*.*)|*.*",
            DefaultExt = ".csx",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true,
            InitialDirectory = GetInitialDirectory(currentPath)
        };
        return dialog.ShowDialog() == true
            ? Load(dialog.FileName)
            : null;
    }

    public ScriptDocument Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return new ScriptDocument(fullPath, File.ReadAllText(fullPath));
    }

    public string? OpenWorkspace(string? currentWorkspace)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open script folder",
            Multiselect = false,
            InitialDirectory = Directory.Exists(currentWorkspace)
                ? currentWorkspace
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public Models.ScriptWorkspaceItem GetWorkspaceRoot(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = fullPath;
        }

        return new Models.ScriptWorkspaceItem(
            name.ToUpperInvariant(),
            fullPath,
            true,
            EnumerateWorkspaceItems(fullPath),
            true);
    }

    public ScriptDocument? CreateInWorkspace(string workspacePath)
    {
        var dialog = new CreateWorkspaceItemDialog(workspacePath, false)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true || dialog.CreatedPath is null)
        {
            return null;
        }

        return Load(dialog.CreatedPath);
    }

    public string? CreateFolderInWorkspace(string workspacePath)
    {
        var dialog = new CreateWorkspaceItemDialog(workspacePath, true)
        {
            Owner = Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.CreatedPath : null;
    }

    public bool DeleteWorkspaceItem(string workspacePath, Models.ScriptWorkspaceItem item)
    {
        var workspaceRoot = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var itemPath = Path.GetFullPath(item.FullPath);
        if (item.IsWorkspaceRoot ||
            !itemPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only items inside the current workspace can be deleted.");
        }

        var dialog = new ConfirmDeleteDialog(item.Name, item.IsFolder)
        {
            Owner = Application.Current?.MainWindow
        };
        
        
if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (item.IsFolder)
        {
            Directory.Delete(itemPath, true);
        }
        else
        {
            File.Delete(itemPath);
        }

        return true;
    }

    public string? RenameWorkspaceItem(string workspacePath, Models.ScriptWorkspaceItem item)
    {
        var workspaceRoot = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var itemPath = Path.GetFullPath(item.FullPath);
        if (item.IsWorkspaceRoot ||
            !itemPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only items inside the current workspace can be renamed.");
        }

        var parentDirectory = Path.GetDirectoryName(itemPath)
                              ?? throw new InvalidOperationException("The workspace item has no parent folder.");
        var dialog = new CreateWorkspaceItemDialog(parentDirectory, item.IsFolder, itemPath)
        {
            Owner = Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.CreatedPath : null;
    }

    public string? Save(string? currentPath, string content)
    {
        var path = currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "C# Script (*.csx)|*.csx|All files (*.*)|*.*",
                DefaultExt = ".csx",
                FileName = "script.csx",
                InitialDirectory = AppContext.BaseDirectory
            };
            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            path = dialog.FileName;
        }

        File.WriteAllText(path, content);
        return path;
    }

    private static string GetInitialDirectory(string? currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var currentDirectory = Path.GetDirectoryName(currentPath);
            if (Directory.Exists(currentDirectory))
            {
                return currentDirectory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static IReadOnlyList<Models.ScriptWorkspaceItem> EnumerateWorkspaceItems(string directory)
    {
        try
        {
            var folders = Directory.EnumerateDirectories(directory)
                .Where(path => !IgnoredDirectories.Contains(Path.GetFileName(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Select(path => new Models.ScriptWorkspaceItem(
                    Path.GetFileName(path), path, true, EnumerateWorkspaceItems(path)));
            var scripts = Directory.EnumerateFiles(directory, "*.csx")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Select(path => new Models.ScriptWorkspaceItem(
                    Path.GetFileName(path), path, false, []));

            return folders.Concat(scripts).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}

public sealed record ScriptDocument(string Path, string Content);
