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

    /// <summary>底部作者、版权或联系信息，可按项目需要修改。</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _footerText = "© 2026 MachineApplication. Designed & Developed by XIOA";
    /// <summary>菜单以树结构配置，可在 Children 中继续增加层级。</summary>
    public MainWindowViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        NavigationItems = RouterConfiguration.Load();
        // 弱订阅避免语言服务一直持有已释放模块的 VM。
        PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
    }
    public IReadOnlyList<NavModel> NavigationItems { get; private set; }

    public void ReloadNavigation()
    {
        NavigationItems = RouterConfiguration.Load();
        OnPropertyChanged(nameof(NavigationItems));
    }

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
        try { _navigation.Navigate("MainRegion", url); }
        catch (UnauthorizedAccessException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "权限不足");
            return;
        }
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

