using System.IO;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using CsxPad.Wpf.Services;

namespace CsxPad.Wpf.Dialogs;

public partial class CreateWorkspaceItemDialog : Window
{
    private readonly bool _isFolder;
    private readonly string? _existingPath;
    private readonly bool _isRename;

    public CreateWorkspaceItemDialog(string targetDirectory, bool isFolder, string? existingPath = null)
    {
        _isFolder = isFolder;
        _existingPath = existingPath is null ? null : Path.GetFullPath(existingPath);
        _isRename = _existingPath is not null;
        TargetDirectory = Path.GetFullPath(targetDirectory);
        DialogTitle = _isRename ? $"Rename {(isFolder ? "Folder" : "C# Script")}" : isFolder ? "New Folder" : "New C# Script";
        InputLabel = _isRename ? "Name" : isFolder ? "Folder" : "Name";
        ConfirmText = _isRename ? "Rename" : "Create";
        InitializeComponent();
        DialogIcon = isFolder ? PackIconKind.FolderOutline : PackIconKind.FileCodeOutline;
        ItemPathBox.Text = _isRename
            ? Path.GetFileName(_existingPath)
            : GetAvailableName(TargetDirectory, isFolder);
        ItemPathBox.SelectAll();
        Loaded += (_, _) => ItemPathBox.Focus();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => DialogResult = false));
        Validate();
    }

    public string TargetDirectory { get; }

    public string DialogTitle { get; }

    public string InputLabel { get; }

    public string ConfirmText { get; }

    public static readonly DependencyProperty DialogIconProperty = DependencyProperty.Register(
        nameof(DialogIcon), typeof(PackIconKind), typeof(CreateWorkspaceItemDialog));

    public PackIconKind DialogIcon
    {
        get => (PackIconKind)GetValue(DialogIconProperty);
        private set => SetValue(DialogIconProperty, value);
    }

    public string? CreatedPath { get; private set; }

    private void ItemPathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Validate();

    private void ItemPathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && CreateButton.IsEnabled)
        {
            CreateItem();
            e.Handled = true;
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e) => CreateItem();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void CreateItem()
    {
        var resolution = ResolveInput();
        if (!resolution.IsValid || resolution.FullPath is null)
        {
            ValidationText.Text = resolution.Error ?? "Invalid path.";
            return;
        }

        try
        {
            var path = resolution.FullPath;
            if (_isRename && _existingPath is not null)
            {
                MoveItem(_existingPath, path, _isFolder);
            }
            else if (_isFolder)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            CreatedPath = path;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ValidationText.Text = exception.Message;
        }
    }

    private void Validate()
    {
        if (!IsInitialized || ItemPathBox is null || CreateButton is null)
        {
            return;
        }

        var resolution = ResolveInput();
        ValidationText.Text = resolution.Error ?? string.Empty;
        CreateButton.IsEnabled = resolution.IsValid;
    }

    private WorkspacePathResolution ResolveInput()
    {
        if (_isRename && (ItemPathBox.Text.Contains('/') || ItemPathBox.Text.Contains('\\')))
        {
            return WorkspacePathResolution.Failed("Enter a name without a folder path.");
        }

        var resolution = WorkspaceItemPathResolver.Resolve(
            TargetDirectory,
            ItemPathBox.Text,
            _isFolder,
            _existingPath);
        return _isRename && resolution.FullPath is not null &&
               string.Equals(resolution.FullPath, _existingPath, StringComparison.Ordinal)
            ? WorkspacePathResolution.Failed("Enter a different name.")
            : resolution;
    }

    private static void MoveItem(string source, string destination, bool isFolder)
    {
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            if (isFolder)
            {
                Directory.Move(source, destination);
            }
            else
            {
                File.Move(source, destination);
            }

            return;
        }

        var parent = Path.GetDirectoryName(source)!;
        var temporaryPath = Path.Combine(parent, $".csxpad-rename-{Guid.NewGuid():N}");
        if (!isFolder)
        {
            temporaryPath += Path.GetExtension(source);
        }

        if (isFolder)
        {
            Directory.Move(source, temporaryPath);
        }
        else
        {
            File.Move(source, temporaryPath);
        }

        try
        {
            if (isFolder)
            {
                Directory.Move(temporaryPath, destination);
            }
            else
            {
                File.Move(temporaryPath, destination);
            }
        }
        catch
        {
            if (isFolder)
            {
                Directory.Move(temporaryPath, source);
            }
            else
            {
                File.Move(temporaryPath, source);
            }

            throw;
        }
    }

    private static string GetAvailableName(string directory, bool isFolder)
    {
        var baseName = isFolder ? "folder" : "script";
        var extension = isFolder ? string.Empty : ".csx";
        for (var index = 0; ; index++)
        {
            var candidate = $"{baseName}{(index == 0 ? string.Empty : index)}{extension}";
            var path = Path.Combine(directory, candidate);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return candidate;
            }
        }
    }
}
