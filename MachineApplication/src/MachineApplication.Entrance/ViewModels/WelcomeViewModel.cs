using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Utils;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>欢迎页入口，进入已注册的主页工作区域。</summary>
public partial class WelcomeViewModel(INavigationService navigation) : ObservableObject
{
    [ObservableProperty] private string _status = "";

    /// <summary>通过统一导航服务进入主页，访问权限仍由导航层检查。</summary>
    [RelayCommand]
    private void EnterWorkspace()
    {
        try { navigation.Navigate("MainRegion", "home"); Status = ""; }
        catch (Exception ex) { Status = ManagementMessages.Error(ex); }
    }
}
