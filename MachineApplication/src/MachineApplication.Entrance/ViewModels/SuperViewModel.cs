using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Region;
using Machine.ModuleLoad.Mvvm;
using Machine.ModuleLoad.Utils;
using Machine.ModuleLoad.StartupTool;
using MachineApplication.Entrance.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MachineApplication.Entrance.ViewModels;

public partial class SuperViewModel : MachineViewModelBase, IAsyncNavigationAware {
    private const double MinimumMainWindowWidth = 640;
    private const double MinimumMainWindowHeight = 400;
    private const double MaximumMainWindowDimension = 10000;
    private JsonObject? _document;
    private string? _loadedText;
    private Func<string> _statusText = () => string.Empty;
    private readonly MainWindowSettings _mainWindowSettings;

    private readonly WindowsStartupRegistration _startupRegistration = new(
        Environment.ProcessPath ??
        throw new InvalidOperationException("Cannot determine the application executable path."));

    public string ConfigurationPath { get; } = Path.Combine(AppContext.BaseDirectory, "appSettings.json");
    public string ActiveProvider { get; }

    public string ConsoleStatusText => IsConsoleOpen
        ? ViewModelLocator.EntranceLang.SuperPage_ConsoleOpen
        : ViewModelLocator.EntranceLang.SuperPage_ConsoleClosed;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ConsoleStatusText))]
    private bool _isConsoleOpen;

    [ObservableProperty] private bool _consoleHasError;
    [ObservableProperty] private string _consoleMessage = "";
    private Func<string> _consoleMessageText = () => string.Empty;

    public string ActiveProviderText =>
        string.Format(ViewModelLocator.EntranceLang.SuperPage_ActiveProvider, ActiveProvider);

    public LoggingProvider[] Providers { get; } = Enum.GetValues<LoggingProvider>();
    public LoggingLevel[] Levels { get; } = Enum.GetValues<LoggingLevel>();

    /// <summary>主窗口启动方式：Normal 显示欢迎页，Index 自动进入首个可访问页面。</summary>
    [ObservableProperty] private string _indexPage = "Normal";

    [ObservableProperty] private bool _startWithWindows;

    [ObservableProperty] private string _mainWindowWidth = "1040";
    [ObservableProperty] private string _mainWindowHeight = "640";

    [ObservableProperty] private LoggingProvider _provider;
    [ObservableProperty] private LoggingLevel _minimumLevel;
    [ObservableProperty] private string _logDirectory = "Logs";
    [ObservableProperty] private string _fileSizeLimitBytes = "10485760";
    [ObservableProperty] private string _retainedFileCountLimit = "31";
    [ObservableProperty] private string _retentionDays = "30";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasError;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isLoaded;

    private readonly ISnackBar _snackBar;

    public SuperViewModel(IOptions<LoggingOptions> loggerOptions, IOptions<MainWindowSettings> mainWindowSettings,
        ISnackBar snackBar) {
        ActiveProvider = loggerOptions.Value.Provider.ToString();
        _snackBar = snackBar;
        _mainWindowSettings = mainWindowSettings.Value;
        IsConsoleOpen = CmdTools.IsConsoleOpen;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(
            ViewModelLocator.EntranceLang, OnDisplayLanguageChanged, string.Empty);
    }

    public Task PrepareAsync(RegionNavigationContext context, CancellationToken cancellationToken)
        => IsLoaded ? Task.CompletedTask : ReloadAsync(cancellationToken);

    [RelayCommand]
    private void OpenConsole() {
        try {
            CmdTools.OpenConsole();
            ConsoleHasError = false;
            SetConsoleMessage(() => ViewModelLocator.EntranceLang.SuperPage_ConsoleOpened);
            GlobalLogger.DebuggerLogger?.Success(ViewModelLocator.EntranceLang.SuperPage_ConsoleOpened);
        }
        catch (Exception exception) {
            ConsoleHasError = true;
            SetConsoleMessage(() =>
                string.Format(ViewModelLocator.EntranceLang.SuperPage_ConsoleFailed, exception.Message));
        }
        finally {
            IsConsoleOpen = CmdTools.IsConsoleOpen;
        }
    }

    [RelayCommand]
    private void CloseConsole() {
        try {
            CmdTools.CloseConsole();
            ConsoleHasError = false;
            SetConsoleMessage(() => ViewModelLocator.EntranceLang.SuperPage_ConsoleClosed);
        }
        catch (Exception exception) {
            ConsoleHasError = true;
            SetConsoleMessage(() =>
                string.Format(ViewModelLocator.EntranceLang.SuperPage_ConsoleFailed, exception.Message));
        }
        finally {
            IsConsoleOpen = CmdTools.IsConsoleOpen;
        }
    }

    private void SetConsoleMessage(Func<string> text) {
        _consoleMessageText = text;
        ConsoleMessage = text();
    }

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken) {
        try {
            string? text;
            try {
                text = await File.ReadAllTextAsync(ConfigurationPath, cancellationToken);
            }
            catch (FileNotFoundException) {
                text = null;
            }

            var (document, options, indexPage, windowWidth, windowHeight) =
                await Task.Run(() => ParseConfiguration(text), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StartWithWindows = _startupRegistration.IsEnabled();
            ApplyOptions(options);
            IndexPage = indexPage;
            MainWindowWidth = windowWidth.ToString("0.##", CultureInfo.InvariantCulture);
            MainWindowHeight = windowHeight.ToString("0.##", CultureInfo.InvariantCulture);
            _document = document;
            _loadedText = text;
            IsLoaded = true;
            HasError = false;
            SetStatus(() => text is null
                ? ViewModelLocator.EntranceLang.SuperPage_FileMissing
                : ViewModelLocator.EntranceLang.SuperPage_Loaded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            IsLoaded = false;
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_ReadFailed, exception.Message));
        }
    }

    private static (JsonObject Document, LoggingOptions Options, string IndexPage, double WindowWidth, double
        WindowHeight)
        ParseConfiguration(string? text) {
        var document = text is null
            ? new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
            : JsonNode.Parse(text,
                  new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                  new JsonDocumentOptions {
                      AllowTrailingCommas = true,
                      CommentHandling = JsonCommentHandling.Skip
                  }) as JsonObject
              ?? throw new LocalizedConfigurationException(() =>
                  ViewModelLocator.EntranceLang.SuperPage_RootObjectRequired);
        if (document["Logging"] is not null and not JsonObject)
            throw new LocalizedConfigurationException(() =>
                ViewModelLocator.EntranceLang.SuperPage_LoggingObjectRequired);
        if (document["Startup"] is not null and not JsonObject)
            throw new LocalizedConfigurationException(() =>
                ViewModelLocator.EntranceLang.SuperPage_StartupObjectRequired);
        if (document[MainWindowSettings.SectionName] is not null and not JsonObject)
            throw new LocalizedConfigurationException(() =>
                ViewModelLocator.EntranceLang.SuperPage_MainWindowObjectRequired);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        using var configurationLifetime = (IDisposable)configuration;
        var options = configuration.GetSection("Logging").Get<LoggingOptions>() ?? new LoggingOptions();
        var windowSettings = configuration.GetSection(MainWindowSettings.SectionName).Get<MainWindowSettings>()
                             ?? new MainWindowSettings();
        ValidateWindowDimension(windowSettings.Width, MinimumMainWindowWidth,
            () => ViewModelLocator.EntranceLang.SuperPage_WindowWidthInvalid);
        ValidateWindowDimension(windowSettings.Height, MinimumMainWindowHeight,
            () => ViewModelLocator.EntranceLang.SuperPage_WindowHeightInvalid);
        // 与主窗口保持一致：仅 Index 开启自动导航，缺失或其他值均按 Normal 回填。
        var indexPage = string.Equals(configuration["Startup:IndexPage"]?.Trim(), "Index",
            StringComparison.OrdinalIgnoreCase)
            ? "Index"
            : "Normal";
        return (document, options, indexPage, windowSettings.Width, windowSettings.Height);
    }

    private static double ParseWindowDimension(string text, double minimum, Func<string> errorText) {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new LocalizedConfigurationException(errorText);
        ValidateWindowDimension(value, minimum, errorText);
        return value;
    }

    private static void ValidateWindowDimension(double value, double minimum, Func<string> errorText) {
        if (!double.IsFinite(value) || value < minimum || value > MaximumMainWindowDimension)
            throw new LocalizedConfigurationException(errorText);
    }

    [RelayCommand]
    private void RestoreDefaults() {
        ApplyOptions(new LoggingOptions());
        IndexPage = "Normal";
        StartWithWindows = false;
        MainWindowWidth = "1040";
        MainWindowHeight = "640";
        HasError = !IsLoaded;
        SetStatus(() => IsLoaded
            ? ViewModelLocator.EntranceLang.SuperPage_DefaultsRestored
            : ViewModelLocator.EntranceLang.SuperPage_DefaultsRequireReload);
    }

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private void Save() {
        try {
            if (IndexPage is not ("Normal" or "Index"))
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_IndexPageInvalid);
            if (!Enum.IsDefined(Provider) || !Enum.IsDefined(MinimumLevel))
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_InvalidSelection);
            if (string.IsNullOrWhiteSpace(LogDirectory))
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_LogDirectoryRequired);
            var directory = LogDirectory.Trim();
            _ = Path.GetFullPath(directory, AppContext.BaseDirectory);
            if (!long.TryParse(FileSizeLimitBytes, out var fileSize) || fileSize <= 0)
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_FileSizeInvalid);
            if (!int.TryParse(RetainedFileCountLimit, out var fileCount) || fileCount <= 0)
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_FileCountInvalid);
            if (!int.TryParse(RetentionDays, out var retentionDays) || retentionDays < 0)
                throw new LocalizedConfigurationException(() =>
                    ViewModelLocator.EntranceLang.SuperPage_RetentionDaysInvalid);
            var windowWidth = ParseWindowDimension(MainWindowWidth, MinimumMainWindowWidth,
                () => ViewModelLocator.EntranceLang.SuperPage_WindowWidthInvalid);
            var windowHeight = ParseWindowDimension(MainWindowHeight, MinimumMainWindowHeight,
                () => ViewModelLocator.EntranceLang.SuperPage_WindowHeightInvalid);

            var document = (JsonObject)_document!.DeepClone();
            var logging = document["Logging"] as JsonObject;
            if (logging is null) {
                logging = new JsonObject();
                document["Logging"] = logging;
            }

            logging["Provider"] = Provider.ToString();
            logging["MinimumLevel"] = MinimumLevel.ToString();
            logging["LogDirectory"] = directory;
            logging["FileSizeLimitBytes"] = fileSize;
            logging["RetainedFileCountLimit"] = fileCount;
            logging["RetentionDays"] = retentionDays;
            // 在原配置副本中只更新目标字段，保留 Startup 的其他字段和不相关配置节。
            var startup = document["Startup"] as JsonObject;
            if (startup is null) {
                startup = new JsonObject();
                document["Startup"] = startup;
            }

            startup["IndexPage"] = IndexPage;
            var mainWindow = document[MainWindowSettings.SectionName] as JsonObject;
            if (mainWindow is null) {
                mainWindow = new JsonObject();
                document[MainWindowSettings.SectionName] = mainWindow;
            }

            mainWindow[nameof(MainWindowSettings.Width)] = windowWidth;
            mainWindow[nameof(MainWindowSettings.Height)] = windowHeight;
            if (!string.Equals(ReadConfiguration(), _loadedText, StringComparison.Ordinal))
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_ExternalChange);
            if (_loadedText is not null)
                File.Copy(ConfigurationPath, ConfigurationPath + ".bak", overwrite: true);

            var result = JsonFileUtils.Write(ConfigurationPath, document);
            if (result.IsErr) {
                HasError = true;
                SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_SaveFailed, result.UnwrapErr()));
                return;
            }

            _document = document;
            LogDirectory = directory;
            RetentionDays = retentionDays.ToString(CultureInfo.InvariantCulture);
            // 配置写入成功后同步主窗口设置，动态绑定立即更新当前窗口尺寸。
            _mainWindowSettings.Width = windowWidth;
            _mainWindowSettings.Height = windowHeight;
            try {
                _loadedText = File.ReadAllText(ConfigurationPath);
                try {
                    _startupRegistration.SetEnabled(StartWithWindows);
                }
                catch (Exception exception) {
                    HasError = true;
                    SetStatus(() =>
                        string.Format(ViewModelLocator.EntranceLang.SuperPage_StartupSaveFailed, exception.Message));
                    return;
                }

                HasError = false;
                SetStatus(() => ViewModelLocator.EntranceLang.SuperPage_Saved);
                _snackBar.SendMessage(ViewModelLocator.EntranceLang.SuperPage_Saved,5000);
            }
            catch (Exception exception) {
                IsLoaded = false;
                HasError = true;
                SetStatus(() =>
                    string.Format(ViewModelLocator.EntranceLang.SuperPage_SavedReloadFailed, exception.Message));
            }
        }
        catch (Exception exception) {
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_SaveFailed, exception.Message));
        }
    }

    private void SetStatus(Func<string> text) {
        _statusText = text;
        Status = text();
    }

    private void OnDisplayLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) {
        Status = _statusText();
        OnPropertyChanged(nameof(ActiveProviderText));
        OnPropertyChanged(nameof(ConsoleStatusText));
        ConsoleMessage = _consoleMessageText();
    }

    private sealed class LocalizedConfigurationException(Func<string> message) : Exception {
        public override string Message => message();
    }

    private string? ReadConfiguration() {
        try {
            return File.ReadAllText(ConfigurationPath);
        }
        catch (FileNotFoundException) {
            return null;
        }
    }

    private void ApplyOptions(LoggingOptions options) {
        Provider = options.Provider;
        MinimumLevel = options.MinimumLevel;
        LogDirectory = options.LogDirectory;
        FileSizeLimitBytes = options.FileSizeLimitBytes.ToString(CultureInfo.InvariantCulture);
        RetainedFileCountLimit = options.RetainedFileCountLimit.ToString(CultureInfo.InvariantCulture);
        RetentionDays = options.RetentionDays.ToString(CultureInfo.InvariantCulture);
    }
}