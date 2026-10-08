using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsxPad.Wpf.Models;
using CsxPad.Wpf.Scripting;
using CsxPad.Wpf.Services;

namespace CsxPad.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly NuGetWorkspaceService _nuGetWorkspace;
    private readonly CSharpScriptService _scriptService;
    private readonly ScriptFileService _fileService = new();
    private readonly WorkspaceSessionService _sessionService;
    private CancellationTokenSource? _runCancellation;
    private ScriptDebugSession? _debugSession;
    private bool _isLoadingDocument;
    private bool _usesAspNetCoreFramework;

    public MainWindowViewModel(WorkspaceSessionService? sessionService = null, string? nodeScript = null,
        string nodeTitle = "node.csx", string? nodeScriptPath = null, string? packageWorkspaceDirectory = null)
    {
        _sessionService = sessionService ?? new WorkspaceSessionService();
        _nuGetWorkspace = new NuGetWorkspaceService(packageWorkspaceDirectory);
        _scriptService = new CSharpScriptService(packageWorkspaceDirectory: packageWorkspaceDirectory);
        ResultView = CreateEmptyView();
        IsNodeDocument = nodeScript is not null;
        if (nodeScript is null) RestoreWorkspaceSession();
        else
        {
            LoadDocument(nodeScriptPath, nodeScript);
            DocumentTitle = nodeTitle;
            if (nodeScriptPath is not null) SetWorkspaceFromDocument(nodeScriptPath);
        }
        _usesAspNetCoreFramework = NuGetWorkspaceService.UsesAspNetCoreFramework(ScriptText);
        RefreshInstalledPackages();
        _ = RefreshPackageCompletionCatalogAsync();
    }

    public ObservableCollection<InstalledPackageItem> InstalledPackages { get; } = [];

    public bool IsNodeDocument { get; }
    public event Action<string>? ApplyNodeScriptRequested;

    [RelayCommand(CanExecute = nameof(CanApplyNodeScript))]
    private void ApplyNodeScript() => ApplyNodeScriptRequested?.Invoke(ScriptText);

    private bool CanApplyNodeScript() => IsNodeDocument && !IsRunning;

    public ObservableCollection<NuGetPackageItem> PackageResults { get; } = [];

    public ObservableCollection<ScriptWorkspaceItem> WorkspaceItems { get; } = [];

    public ObservableCollection<int> Breakpoints { get; } = [];

    public string PackageCountText => $"{InstalledPackages.Count} installed";

    public bool HasInstalledPackages => InstalledPackages.Count > 0;

    public string WindowTitle => $"{DocumentTitle}{(IsDirty ? " *" : string.Empty)} - CsxPad";

    public string DocumentLocation => string.IsNullOrWhiteSpace(DocumentPath)
        ? "Local C# script"
        : Path.GetDirectoryName(DocumentPath) ?? DocumentPath;

    public string CurrentScriptPath => DocumentPath ?? Path.Combine(
        WorkspacePath ?? Environment.CurrentDirectory,
        DocumentTitle);

    public string WorkspaceName => string.IsNullOrWhiteSpace(WorkspacePath)
        ? "NO FOLDER OPENED"
        : Path.GetFileName(WorkspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public bool IsLeftPaneOpen => LeftToolPane is not null;

    public bool IsExplorerOpen => LeftToolPane == "Explorer";

    public bool IsPackagesOpen => LeftToolPane == "Packages";

    public bool IsBottomPaneOpen => BottomToolPane is not null;

    public bool IsResultsOpen => BottomToolPane == "Results";

    public bool IsOutputOpen => BottomToolPane == "Output";

    [ObservableProperty]
    private string? _leftToolPane = "Explorer";

    [ObservableProperty]
    private bool _isRightPaneOpen = true;

    [ObservableProperty]
    private string? _bottomToolPane = "Results";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceScriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceFolderCommand))]
    private string? _workspacePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(DebugCommand))]
    private string _scriptText = """
        var values = Enumerable.Range(1, 12)
            .Where(number => number % 2 == 0)
            .Select(number => new
            {
                Number = number,
                Square = number * number,
                Label = $"Item {number}"
            });

        values.Dump("Even numbers");
        """;

    [ObservableProperty]
    private string? _documentPath;

    [ObservableProperty]
    private string _documentTitle = "untitled.csx";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(DebugCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyNodeScriptCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueDebugCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepDebugCommand))]
    private bool _isDebugging;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueDebugCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepDebugCommand))]
    private bool _isDebugPaused;

    [ObservableProperty]
    private int? _currentDebugLine;

    [ObservableProperty]
    private ScriptDebugPause? _currentDebugPause;

    [ObservableProperty]
    private bool _isNuGetBusy;

    [ObservableProperty]
    private PackageCompletionCatalog _packageCompletionCatalog = PackageCompletionCatalog.Empty;

    [ObservableProperty]
    private string _packageSearch = string.Empty;

    [ObservableProperty]
    private string _nuGetStatus = "Search NuGet.org or add a #r nuget directive.";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _executionTime = "Not run";

    [ObservableProperty]
    private string _rowCount = "0 rows";

    [ObservableProperty]
    private string _outputText = "Run the script to see output and compiler diagnostics.";

    [ObservableProperty]
    private DataView _resultView;

    [ObservableProperty]
    private IReadOnlyList<ScriptResultSection> _resultSections = [];

    [ObservableProperty]
    private int _selectedResultTab;

    [RelayCommand]
    private void ToggleExplorer() => LeftToolPane = IsExplorerOpen ? null : "Explorer";

    [RelayCommand]
    private void TogglePackages() => LeftToolPane = IsPackagesOpen ? null : "Packages";

    [RelayCommand]
    private void ToggleRightPane() => IsRightPaneOpen = !IsRightPaneOpen;

    [RelayCommand]
    private void ToggleResults()
    {
        if (IsResultsOpen)
        {
            BottomToolPane = null;
            return;
        }

        SelectedResultTab = 0;
        BottomToolPane = "Results";
    }

    [RelayCommand]
    private void ToggleOutput()
    {
        if (IsOutputOpen)
        {
            BottomToolPane = null;
            return;
        }

        SelectedResultTab = 1;
        BottomToolPane = "Output";
    }

    [RelayCommand]
    private void OpenWorkspace()
    {
        var path = _fileService.OpenWorkspace(WorkspacePath);
        if (path is null)
        {
            return;
        }

        WorkspacePath = path;
        RefreshWorkspaceItems();
        LeftToolPane = "Explorer";
        SaveWorkspaceSession();
        StatusMessage = $"Opened folder {path}";
    }

    [RelayCommand(CanExecute = nameof(CanCreateWorkspaceScript))]
    private void CreateWorkspaceScript()
    {
        if (WorkspacePath is null)
        {
            return;
        }

        CreateScriptInDirectory(WorkspacePath);
    }

    [RelayCommand(CanExecute = nameof(CanCreateScriptInFolder))]
    private void CreateScriptInFolder(ScriptWorkspaceItem? folder)
    {
        if (folder is not { IsFolder: true } || !Directory.Exists(folder.FullPath))
        {
            return;
        }

        CreateScriptInDirectory(folder.FullPath);
    }

    private static bool CanCreateScriptInFolder(ScriptWorkspaceItem? folder) =>
        folder is { IsFolder: true } && Directory.Exists(folder.FullPath);

    private void CreateScriptInDirectory(string directory)
    {
        var document = _fileService.CreateInWorkspace(directory);
        if (document is null)
        {
            return;
        }

        RefreshWorkspaceItems();
        LoadDocument(document.Path, document.Content);
        SaveWorkspaceSession();
        StatusMessage = $"Created {document.Path}";
    }

    private bool CanCreateWorkspaceScript() => Directory.Exists(WorkspacePath);

    [RelayCommand(CanExecute = nameof(CanCreateWorkspaceScript))]
    private void CreateWorkspaceFolder()
    {
        if (WorkspacePath is not null)
        {
            CreateFolderInDirectory(WorkspacePath);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateScriptInFolder))]
    private void CreateFolderInFolder(ScriptWorkspaceItem? folder)
    {
        if (folder is { IsFolder: true })
        {
            CreateFolderInDirectory(folder.FullPath);
        }
    }

    private void CreateFolderInDirectory(string directory)
    {
        var path = _fileService.CreateFolderInWorkspace(directory);
        if (path is null)
        {
            return;
        }

        RefreshWorkspaceItems();
        StatusMessage = $"Created folder {path}";
    }

    [RelayCommand(CanExecute = nameof(CanDeleteWorkspaceItem))]
    private void DeleteWorkspaceItem(ScriptWorkspaceItem? item)
    {
        if (item is null || WorkspacePath is null)
        {
            return;
        }

        try
        {
            var containsCurrentDocument = DocumentPath is not null &&
                                          IsPathInsideOrEqual(DocumentPath, item.FullPath);
            if (!_fileService.DeleteWorkspaceItem(WorkspacePath, item))
            {
                return;
            }

            if (containsCurrentDocument)
            {
                DocumentPath = null;
                DocumentTitle = "untitled.csx";
                IsDirty = true;
            }

            RefreshWorkspaceItems();
            SaveWorkspaceSession();
            StatusMessage = $"Deleted {item.Name}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not delete {item.Name}";
            OutputText = exception.Message;
            ShowBottomPane(1);
        }
    }

    private static bool CanDeleteWorkspaceItem(ScriptWorkspaceItem? item) =>
        item is { IsWorkspaceRoot: false } && (File.Exists(item.FullPath) || Directory.Exists(item.FullPath));

    [RelayCommand(CanExecute = nameof(CanRenameWorkspaceItem))]
    private void RenameWorkspaceItem(ScriptWorkspaceItem? item)
    {
        if (item is null || WorkspacePath is null)
        {
            return;
        }

        try
        {
            var oldPath = Path.GetFullPath(item.FullPath);
            var renamedPath = _fileService.RenameWorkspaceItem(WorkspacePath, item);
            if (renamedPath is null)
            {
                return;
            }

            if (DocumentPath is not null && IsPathInsideOrEqual(DocumentPath, oldPath))
            {
                DocumentPath = item.IsFolder
                    ? Path.Combine(renamedPath, Path.GetRelativePath(oldPath, DocumentPath))
                    : renamedPath;
                DocumentTitle = Path.GetFileName(DocumentPath);
            }

            RefreshWorkspaceItems();
            SaveWorkspaceSession();
            StatusMessage = $"Renamed {item.Name} to {Path.GetFileName(renamedPath)}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not rename {item.Name}";
            OutputText = exception.Message;
            ShowBottomPane(1);
        }
    }

    private static bool CanRenameWorkspaceItem(ScriptWorkspaceItem? item) =>
        item is { IsWorkspaceRoot: false } && (File.Exists(item.FullPath) || Directory.Exists(item.FullPath));

    private static bool IsPathInsideOrEqual(string path, string candidateParent)
    {
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetFullPath(candidateParent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, parent, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void OpenWorkspaceItem(ScriptWorkspaceItem? item)
    {
        if (item is null || item.IsFolder)
        {
            return;
        }

        try
        {
            var document = _fileService.Load(item.FullPath);
            LoadDocument(document.Path, document.Content);
            SaveWorkspaceSession();
            StatusMessage = $"Opened {document.Path}";
        }
        catch (Exception exception)
        {
            StatusMessage = "Could not open script";
            OutputText = exception.Message;
            ShowBottomPane(1);
        }
    }

    [RelayCommand]
    private void NewScript()
    {
        LoadDocument(null, string.Empty);
        SaveWorkspaceSession();
        StatusMessage = "New script";
    }

    [RelayCommand]
    private void OpenScript()
    {
        try
        {
            var document = _fileService.Open(DocumentPath);
            if (document is null)
            {
                return;
            }

            LoadDocument(document.Path, document.Content);
            SetWorkspaceFromDocument(document.Path);
            SaveWorkspaceSession();
            StatusMessage = $"Opened {document.Path}";
        }
        catch (Exception exception)
        {
            StatusMessage = "Could not open script";
            OutputText = exception.Message;
            ShowBottomPane(1);
        }
    }

    [RelayCommand]
    private void SaveScript()
    {
        if (IsNodeDocument)
        {
            if (CanApplyNodeScript()) ApplyNodeScript();
            return;
        }
        var savedPath = _fileService.Save(DocumentPath, ScriptText);
        if (savedPath is null)
        {
            return;
        }

        DocumentPath = savedPath;
        DocumentTitle = Path.GetFileName(savedPath);
        IsDirty = false;
        SetWorkspaceFromDocument(savedPath);
        SaveWorkspaceSession();
        StatusMessage = "Script saved";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunAsync() => ExecuteScriptAsync(false);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task DebugAsync() => ExecuteScriptAsync(true);

    private async Task ExecuteScriptAsync(bool debug)
    {
        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var debugSession = debug
            ? new ScriptDebugSession(Breakpoints, _runCancellation.Token)
            : null;
        _debugSession = debugSession;
        if (debugSession is not null)
        {
            debugSession.PausedChanged += OnDebugPauseChanged;
        }

        IsRunning = true;
        IsDebugging = debug;
        StatusMessage = debug ? "Compiling for debug..." : "Compiling and executing...";
        OutputText = debug
            ? $"Preparing debug session with {Breakpoints.Count} breakpoint(s)."
            : "Preparing script and NuGet references.";

        try
        {
            var result = debugSession is null
                ? await _scriptService.RunAsync(ScriptText, CurrentScriptPath, _runCancellation.Token)
                : await _scriptService.DebugAsync(
                    ScriptText,
                    CurrentScriptPath,
                    debugSession,
                    _runCancellation.Token);
            stopwatch.Stop();
            ResultView = result.ResultView;
            ResultSections = result.ResultSections;
            RowCount = $"{result.RowCount} row{(result.RowCount == 1 ? string.Empty : "s")}";
            ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms";
            OutputText = result.Output;
            StatusMessage = result.Success ? "Script completed" : "Script failed";
            ShowBottomPane(result.Success ? 0 : 1);
            RefreshInstalledPackages();
            await RefreshPackageCompletionCatalogAsync();
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            StatusMessage = "Execution canceled";
            OutputText = "The script was canceled by the user.";
            ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms";
            ShowBottomPane(1);
        }
        finally
        {
            if (debugSession is not null)
            {
                debugSession.PausedChanged -= OnDebugPauseChanged;
            }

            _debugSession = null;
            CurrentDebugPause = null;
            CurrentDebugLine = null;
            IsDebugPaused = false;
            IsDebugging = false;
            IsRunning = false;
        }
    }

    private bool CanRun() => !IsRunning && !string.IsNullOrWhiteSpace(ScriptText);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _runCancellation?.Cancel();
    }

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanContinueDebug))]
    private void ContinueDebug() => _debugSession?.Continue();

    private bool CanContinueDebug() => IsDebugging && IsDebugPaused;

    [RelayCommand(CanExecute = nameof(CanContinueDebug))]
    private void StepDebug() => _debugSession?.Step();

    [RelayCommand]
    private void ToggleBreakpoint(int line)
    {
        if (line <= 0 || IsRunning)
        {
            return;
        }

        if (Breakpoints.Contains(line))
        {
            Breakpoints.Remove(line);
            StatusMessage = $"Removed breakpoint at line {line}";
            return;
        }

        var insertAt = 0;
        while (insertAt < Breakpoints.Count && Breakpoints[insertAt] < line)
        {
            insertAt++;
        }

        Breakpoints.Insert(insertAt, line);
        StatusMessage = $"Breakpoint set at line {line}";
    }

    private void OnDebugPauseChanged(ScriptDebugPause? pause)
    {
        void ApplyState()
        {
            CurrentDebugPause = pause;
            CurrentDebugLine = pause?.Line;
            IsDebugPaused = pause is not null;
            if (pause is not null)
            {
                StatusMessage = $"Paused at line {pause.Line} ({pause.Values.Count} variable(s))";
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyState();
        }
        else
        {
            dispatcher.Invoke(ApplyState);
        }
    }

    [RelayCommand]
    private async Task SearchPackagesAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageSearch))
        {
            PackageResults.Clear();
            NuGetStatus = "Enter a package name to search NuGet.org.";
            return;
        }

        IsNuGetBusy = true;
        NuGetStatus = "Searching NuGet.org...";
        try
        {
            var results = await _nuGetWorkspace.SearchAsync(PackageSearch, CancellationToken.None);
            PackageResults.Clear();
            foreach (var package in results)
            {
                PackageResults.Add(package);
            }

            NuGetStatus = $"{results.Count} packages found";
        }
        catch (Exception exception)
        {
            NuGetStatus = exception.Message;
        }
        finally
        {
            IsNuGetBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallPackageAsync(NuGetPackageItem? package)
    {
        if (package is null)
        {
            return;
        }

        IsNuGetBusy = true;
        NuGetStatus = $"Installing {package.Id} {package.Version}...";
        try
        {
            await _nuGetWorkspace.InstallAsync(package.Id, package.Version, CancellationToken.None);
            AddNuGetDirective(package.Id, package.Version);
            RefreshInstalledPackages();
            await RefreshPackageCompletionCatalogAsync();
            NuGetStatus = $"Installed {package.Id} {package.Version}";
        }
        catch (Exception exception)
        {
            NuGetStatus = exception.Message;
        }
        finally
        {
            IsNuGetBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemovePackageAsync(InstalledPackageItem? package)
    {
        if (package is null)
        {
            return;
        }

        IsNuGetBusy = true;
        NuGetStatus = $"Removing {package.Id}...";
        try
        {
            await _nuGetWorkspace.RemoveAsync(package.Id, CancellationToken.None);
            RemoveNuGetDirective(package.Id);
            RefreshInstalledPackages();
            await RefreshPackageCompletionCatalogAsync();
            NuGetStatus = $"Removed {package.Id}";
        }
        catch (Exception exception)
        {
            NuGetStatus = exception.Message;
        }
        finally
        {
            IsNuGetBusy = false;
        }
    }

    [RelayCommand]
    private void AddPackageToScript(InstalledPackageItem? package)
    {
        if (package is null)
        {
            return;
        }

        var added = AddNuGetDirective(package.Id, package.Version);
        NuGetStatus = added
            ? $"Referenced {package.Id} {package.Version} in the current CSX"
            : $"{package.Id} is already referenced by the current CSX";
    }

    partial void OnScriptTextChanged(string value)
    {
        if (!_isLoadingDocument)
        {
            IsDirty = true;
        }

        var usesAspNetCoreFramework = NuGetWorkspaceService.UsesAspNetCoreFramework(value);
        if (usesAspNetCoreFramework != _usesAspNetCoreFramework)
        {
            _usesAspNetCoreFramework = usesAspNetCoreFramework;
            _ = RefreshPackageCompletionCatalogAsync();
        }
    }

    partial void OnDocumentPathChanged(string? value)
    {
        OnPropertyChanged(nameof(DocumentLocation));
        OnPropertyChanged(nameof(CurrentScriptPath));
    }

    partial void OnDocumentTitleChanged(string value)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentScriptPath));
    }

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnWorkspacePathChanged(string? value)
    {
        OnPropertyChanged(nameof(WorkspaceName));
        OnPropertyChanged(nameof(CurrentScriptPath));
    }

    partial void OnLeftToolPaneChanged(string? value)
    {
        OnPropertyChanged(nameof(IsLeftPaneOpen));
        OnPropertyChanged(nameof(IsExplorerOpen));
        OnPropertyChanged(nameof(IsPackagesOpen));
    }

    partial void OnBottomToolPaneChanged(string? value)
    {
        OnPropertyChanged(nameof(IsBottomPaneOpen));
        OnPropertyChanged(nameof(IsResultsOpen));
        OnPropertyChanged(nameof(IsOutputOpen));
    }

    private void LoadDocument(string? path, string content)
    {
        _isLoadingDocument = true;
        try
        {
            DocumentPath = path;
            DocumentTitle = string.IsNullOrWhiteSpace(path) ? "untitled.csx" : Path.GetFileName(path);
            ScriptText = content;
            Breakpoints.Clear();
            IsDirty = false;
        }
        finally
        {
            _isLoadingDocument = false;
        }
    }

    private bool AddNuGetDirective(string packageId, string version)
    {
        if (Regex.IsMatch(ScriptText, $"^\\s*#r\\s+\"nuget:\\s*{Regex.Escape(packageId)}\\s*,", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            return false;
        }

        ScriptText = $"#r \"nuget: {packageId}, {version}\"{Environment.NewLine}{ScriptText}";
        return true;
    }

    private void RemoveNuGetDirective(string packageId)
    {
        ScriptText = Regex.Replace(
            ScriptText,
            $"^\\s*#r\\s+\"nuget:\\s*{Regex.Escape(packageId)}(?:\\s*,[^\"]*)?\"\\s*\\r?\\n?",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    private async Task RefreshPackageCompletionCatalogAsync()
    {
        try
        {
            var paths = await _nuGetWorkspace.GetScriptReferencePathsAsync(
                ScriptText,
                CancellationToken.None,
                CurrentScriptPath);
            PackageCompletionCatalog = await Task.Run(() => PackageCompletionCatalog.Load(paths));
        }
        catch
        {
            PackageCompletionCatalog = PackageCompletionCatalog.Empty;
        }
    }

    private void RefreshInstalledPackages()
    {
        InstalledPackages.Clear();
        foreach (var package in _nuGetWorkspace.GetInstalledPackages())
        {
            InstalledPackages.Add(package);
        }

        OnPropertyChanged(nameof(PackageCountText));
        OnPropertyChanged(nameof(HasInstalledPackages));
    }

    private void RefreshWorkspaceItems()
    {
        WorkspaceItems.Clear();
        if (!Directory.Exists(WorkspacePath))
        {
            return;
        }

        WorkspaceItems.Add(_fileService.GetWorkspaceRoot(WorkspacePath));
    }

    private void SetWorkspaceFromDocument(string documentPath)
    {
        var directory = Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(directory) ||
            string.Equals(directory, WorkspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorkspacePath = directory;
        RefreshWorkspaceItems();
    }

    private void RestoreWorkspaceSession()
    {
        var session = _sessionService.Load();
        if (Directory.Exists(session.WorkspacePath))
        {
            WorkspacePath = Path.GetFullPath(session.WorkspacePath);
            RefreshWorkspaceItems();
        }

        if (!File.Exists(session.DocumentPath))
        {
            return;
        }

        try
        {
            var document = _fileService.Load(session.DocumentPath);
            LoadDocument(document.Path, document.Content);
            if (WorkspacePath is null)
            {
                SetWorkspaceFromDocument(document.Path);
            }

            StatusMessage = $"Restored {document.Path}";
        }
        catch (Exception exception)
        {
            StatusMessage = "Could not restore the previous script";
            OutputText = exception.Message;
            ShowBottomPane(1);
        }
    }

    private void SaveWorkspaceSession()
    {
        if (!IsNodeDocument) _sessionService.Save(WorkspacePath, DocumentPath);
    }

    private void ShowBottomPane(int tabIndex)
    {
        SelectedResultTab = tabIndex;
        BottomToolPane = tabIndex == 0 ? "Results" : "Output";
    }

    private static DataView CreateEmptyView()
    {
        var table = new DataTable();
        table.Columns.Add("Result", typeof(object));
        return table.DefaultView;
    }
}
