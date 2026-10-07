using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Region;
using Machine.ModuleLoad.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace MachineApplication.Entrance.ViewModels;

public partial class HomeViewModel : MachineViewModelBase
{
    private readonly ILoadingBar _loadingBar;
    private readonly ISnackBar _snackBar;
    private Func<string> _statusText = () => string.Empty;

    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _useGlobal;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunLoadingCommand))]
    private bool _isBusy;

    public HomeViewModel([FromKeyedServices("LoadingBar")] ILoadingBar loadingBar, ISnackBar snackBar)
    {
        _loadingBar = loadingBar;
        _snackBar = snackBar;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(
            ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
    }

    private string DemoMessage => string.IsNullOrWhiteSpace(Message)
        ? ViewModelLocator.EntranceLang.HomeDemo_Sample
        : Message.Trim();

    [RelayCommand]
    private void ShowGrowl(GrowlType type) => Execute(() =>
    {
        var info = new GrowlInfo
        {
            Message = DemoMessage,
            Type = type,
            Duration = type == GrowlType.Fatal ? TimeSpan.Zero : TimeSpan.FromSeconds(4)
        };
        if (UseGlobal) Growl.ShowGlobal(info);
        else Growl.Show(info);
        SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_Sent);
    });

    [RelayCommand]
    private void ClearGrowl(string? target) => Execute(() =>
    {
        if (target == "Global") Growl.ClearGlobal();
        else Growl.Clear();
        SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_Cleared);
    });

    [RelayCommand]
    private void ShowSnack() => Execute(() =>
    {
        _snackBar.SendMessage(DemoMessage, 3000);
        SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_Sent);
    });

    [RelayCommand]
    private void QueueSnack() => Execute(() =>
    {
        var message = DemoMessage;
        for (var index = 1; index <= 3; index++)
            _snackBar.SendMessage(string.Format(ViewModelLocator.EntranceLang.HomeDemo_QueueMessage, message, index), 2000);
        SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_Sent);
    });

    private bool CanRunLoading() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunLoading))]
    private async Task RunLoadingAsync(string? mode)
    {
        if (IsBusy) return;
        IsBusy = true;
        HasError = false;
        SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_LoadingBusy);
        var message = DemoMessage;
        var useGlobal = UseGlobal;
        try
        {
            var result = await _loadingBar.LoadingAsync(async () =>
            {
                await Task.Delay(2000);
                if (mode == "Error")
                    throw new InvalidOperationException(ViewModelLocator.EntranceLang.HomeDemo_SimulatedError);
            });
            if (result.IsErr)
            {
                HasError = true;
                if (mode == "Error") SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_LoadingFailed);
                else SetStatus(() => string.Format(ViewModelLocator.EntranceLang.HomeDemo_Failed, result.UnwrapErr().Message));
                return;
            }
            SetStatus(() => ViewModelLocator.EntranceLang.HomeDemo_LoadingDone);
            if (mode == "Notify")
            {
                if (useGlobal) Growl.SuccessGlobal(message);
                else Growl.Success(message);
                _snackBar.SendMessage(message, 3000);
            }
        }
        catch (Exception exception)
        {
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.HomeDemo_Failed, exception.Message));
        }
        finally { IsBusy = false; }
    }

    private void Execute(Action action)
    {
        try
        {
            HasError = false;
            action();
        }
        catch (Exception exception)
        {
            HasError = true;
            SetStatus(() => string.Format(ViewModelLocator.EntranceLang.HomeDemo_Failed, exception.Message));
        }
    }

    private void SetStatus(Func<string> text)
    {
        _statusText = text;
        Status = text();
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Status = _statusText();
}
