using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Utils;
using Machine.ModuleLoad.StartupTool;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MachineApplication.Entrance.ViewModels;

public partial class SuperViewModel : MachineViewModelBase
{
    private JsonObject? _document;
    private string? _loadedText;
    private Func<string> _statusText = () => string.Empty;

    public string ConfigurationPath { get; } = Path.Combine(AppContext.BaseDirectory, "appSettings.json");
    public string ActiveProvider { get; }
    public string ConsoleStatusText => IsConsoleOpen
        ? ViewModelLocator.EntranceLang.SuperPage_ConsoleOpen
        : ViewModelLocator.EntranceLang.SuperPage_ConsoleClosed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConsoleStatusText))]
    private bool _isConsoleOpen;

    [ObservableProperty] private bool _consoleHasError;
    [ObservableProperty] private string _consoleMessage = "";
    private Func<string> _consoleMessageText = () => string.Empty;
    public string ActiveProviderText => string.Format(ViewModelLocator.EntranceLang.SuperPage_ActiveProvider, ActiveProvider);
    public LoggingProvider[] Providers { get; } = Enum.GetValues<LoggingProvider>();
    public LoggingLevel[] Levels { get; } = Enum.GetValues<LoggingLevel>();

    [ObservableProperty] private LoggingProvider _provider;
    [ObservableProperty] private LoggingLevel _minimumLevel;
    [ObservableProperty] private string _logDirectory = "Logs";
    [ObservableProperty] private string _fileSizeLimitBytes = "10485760";
    [ObservableProperty] private string _retainedFileCountLimit = "31";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isLoaded;

    public SuperViewModel(IOptions<LoggingOptions> options)
    {
        ActiveProvider = options.Value.Provider.ToString();
        IsConsoleOpen = CmdTools.IsConsoleOpen;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(
            ViewModelLocator.EntranceLang, OnDisplayLanguageChanged, string.Empty);
        Reload();
    }

    [RelayCommand]
    private void OpenConsole()
    {
        try
        {
            CmdTools.OpenConsole();
            ConsoleHasError = false;
            SetConsoleMessage(() => ViewModelLocator.EntranceLang.SuperPage_ConsoleOpened);
            GlobalLogger.DebuggerLogger?.Success(ViewModelLocator.EntranceLang.SuperPage_ConsoleOpened);
        }
        catch (Exception exception)
        {
            ConsoleHasError = true;
            SetConsoleMessage(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_ConsoleFailed, exception.Message));
        }
        finally { IsConsoleOpen = CmdTools.IsConsoleOpen; }
    }

    [RelayCommand]
    private void CloseConsole()
    {
        try
        {
            CmdTools.CloseConsole();
            ConsoleHasError = false;
            SetConsoleMessage(() => ViewModelLocator.EntranceLang.SuperPage_ConsoleClosed);
        }
        catch (Exception exception)
        {
            ConsoleHasError = true;
            SetConsoleMessage(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_ConsoleFailed, exception.Message));
        }
        finally { IsConsoleOpen = CmdTools.IsConsoleOpen; }
    }

    private void SetConsoleMessage(Func<string> text)
    {
        _consoleMessageText = text;
        ConsoleMessage = text();
    }

    [RelayCommand]
    private void Reload()
    {
        try
        {
            var text = ReadConfiguration();
            var document = text is null
                ? new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
                : JsonNode.Parse(text,
                    new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    }) as JsonObject
                  ?? throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_RootObjectRequired);
            if (document["Logging"] is not null and not JsonObject)
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_LoggingObjectRequired);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
            var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
            using var configurationLifetime = (IDisposable)configuration;
            var options = configuration.GetSection("Logging").Get<LoggingOptions>() ?? new LoggingOptions();
            ApplyOptions(options);
            _document = document;
            _loadedText = text;
            IsLoaded = true;
            HasError = false;
            SetStatus(() => text is null
                ? ViewModelLocator.EntranceLang.SuperPage_FileMissing
                : ViewModelLocator.EntranceLang.SuperPage_Loaded);
        }
        catch (Exception exception)
        {
            IsLoaded = false;
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_ReadFailed, exception.Message));
        }
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        ApplyOptions(new LoggingOptions());
        HasError = !IsLoaded;
        SetStatus(() => IsLoaded
            ? ViewModelLocator.EntranceLang.SuperPage_DefaultsRestored
            : ViewModelLocator.EntranceLang.SuperPage_DefaultsRequireReload);
    }

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private void Save()
    {
        try
        {
            if (!Enum.IsDefined(Provider) || !Enum.IsDefined(MinimumLevel))
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_InvalidSelection);
            if (string.IsNullOrWhiteSpace(LogDirectory))
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_LogDirectoryRequired);
            var directory = LogDirectory.Trim();
            _ = Path.GetFullPath(directory, AppContext.BaseDirectory);
            if (!long.TryParse(FileSizeLimitBytes, out var fileSize) || fileSize <= 0)
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_FileSizeInvalid);
            if (!int.TryParse(RetainedFileCountLimit, out var fileCount) || fileCount <= 0)
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_FileCountInvalid);

            var document = (JsonObject)_document!.DeepClone();
            var logging = document["Logging"] as JsonObject;
            if (logging is null)
            {
                logging = new JsonObject();
                document["Logging"] = logging;
            }
            logging["Provider"] = Provider.ToString();
            logging["MinimumLevel"] = MinimumLevel.ToString();
            logging["LogDirectory"] = directory;
            logging["FileSizeLimitBytes"] = fileSize;
            logging["RetainedFileCountLimit"] = fileCount;
            if (!string.Equals(ReadConfiguration(), _loadedText, StringComparison.Ordinal))
                throw new LocalizedConfigurationException(() => ViewModelLocator.EntranceLang.SuperPage_ExternalChange);
            if (_loadedText is not null)
                File.Copy(ConfigurationPath, ConfigurationPath + ".bak", overwrite: true);

            var result = JsonFileUtils.Write(ConfigurationPath, document);
            if (result.IsErr)
            {
                HasError = true;
                SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_SaveFailed, result.UnwrapErr()));
                return;
            }

            _document = document;
            LogDirectory = directory;
            try
            {
                _loadedText = File.ReadAllText(ConfigurationPath);
                HasError = false;
                SetStatus(() => ViewModelLocator.EntranceLang.SuperPage_Saved);
            }
            catch (Exception exception)
            {
                IsLoaded = false;
                HasError = true;
                SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_SavedReloadFailed, exception.Message));
            }
        }
        catch (Exception exception)
        {
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.SuperPage_SaveFailed, exception.Message));
        }
    }

    private void SetStatus(Func<string> text)
    {
        _statusText = text;
        Status = text();
    }

    private void OnDisplayLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        Status = _statusText();
        OnPropertyChanged(nameof(ActiveProviderText));
        OnPropertyChanged(nameof(ConsoleStatusText));
        ConsoleMessage = _consoleMessageText();
    }

    private sealed class LocalizedConfigurationException(Func<string> message) : Exception
    {
        public override string Message => message();
    }

    private string? ReadConfiguration()
    {
        try { return File.ReadAllText(ConfigurationPath); }
        catch (FileNotFoundException) { return null; }
    }

    private void ApplyOptions(LoggingOptions options)
    {
        Provider = options.Provider;
        MinimumLevel = options.MinimumLevel;
        LogDirectory = options.LogDirectory;
        FileSizeLimitBytes = options.FileSizeLimitBytes.ToString(CultureInfo.InvariantCulture);
        RetainedFileCountLimit = options.RetainedFileCountLimit.ToString(CultureInfo.InvariantCulture);
    }
}
