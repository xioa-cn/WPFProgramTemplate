using Machine.ModuleLoad.Region;
using CommunityToolkit.Mvvm.Input;
using I18nExtensions;
using MachineApplication.Entrance.Models;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;

namespace MachineApplication.Entrance.ViewModels;

public partial class MainWindowViewModel : MachineViewModelBase
{
    private readonly INavigationService _navigation;
    /// <summary>菜单以树结构配置，可在 Children 中继续增加层级。</summary>
    public MainWindowViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        NavigationItems =
        [
            new NavModel("MainWindow_Home", PackIconKind.ViewDashboardOutline, "home"),
            new NavModel("MainWindow_Settings", PackIconKind.CogOutline, null,
                new NavModel("MainWindow_General", PackIconKind.Tune, "settings"),
                new NavModel("MainWindow_Appearance", PackIconKind.PaletteOutline, null,
                    new NavModel("MainWindow_ThemeColor", PackIconKind.Palette, "theme/colors"))),
           
        ];
        // 弱订阅避免语言服务一直持有已释放模块的 VM。
        PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
    }
    public IReadOnlyList<NavModel> NavigationItems { get; }

    /// <summary>分组按钮展开子菜单，页面按钮通过 URL 导航。</summary>
    [RelayCommand]
    private void ActivateNavigation(NavModel? item)
    {
        if (item is null) return;
        if (item.HasChildren) item.IsExpanded = !item.IsExpanded;
        else if (item.Url is not null) NavigateTo(item.Url);
    }

    /// <summary>导航成功后更新整棵菜单，快捷入口也复用此方法。</summary>
    private void NavigateTo(string url)
    {
        _navigation.Navigate("MainRegion", url);
        foreach (var item in NavigationItems) item.SelectRoute(url);
    }

    /// <summary>即时刷新各层级菜单的中英文名称。</summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var item in NavigationItems) item.RefreshLanguage();
    }
    [RelayCommand]
    private void ShowHome() => NavigateTo("home");

    [RelayCommand]
    private void ShowSettings() => NavigateTo("settings");

    [RelayCommand]
    private void ShowThemeColors() => NavigateTo("theme/colors");

    [RelayCommand]
    private void ChangeLanguage()
    {
        var language = LanguageManager.Instance.CurrentCulture == "zh" ? "en" : "zh";
        LanguageManager.Instance.ChangeLang(language);
    }
}

