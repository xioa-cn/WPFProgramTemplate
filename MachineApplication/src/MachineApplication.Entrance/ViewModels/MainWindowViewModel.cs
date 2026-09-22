using System.Collections.ObjectModel;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Region;
using CommunityToolkit.Mvvm.Input;
using I18nExtensions;
using MachineApplication.Entrance.Models;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>主窗口视图模型，负责菜单加载、区域导航与语言切换。</summary>
public partial class MainWindowViewModel : MachineViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>底部作者、版权或联系信息，可按项目需要修改。</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _footerText = "© 2026 MachineApplication. Designed & Developed by XIOA";
    /// <summary>菜单以树结构配置，可在 Children 中继续增加层级。</summary>
    public MainWindowViewModel(INavigationService navigation, PermissionService permissions)
    {
        _navigation = navigation;
        NavigationItems = RouterConfiguration.Load();
        if (navigation is NavigationService routes)
        {
            routes.Navigated += OnRouteNavigated;
            routes.Floated += OnRouteFloated;
        }
        permissions.Changed += (_, _) =>
        {
            OpenTabs.Clear();
            foreach (var item in NavigationItems) item.SelectRoute("");
        };
        // 弱订阅避免语言服务一直持有已释放模块的 VM。
        PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
    }
    public IReadOnlyList<NavModel> NavigationItems { get; private set; }

    /// <summary>重新读取路由菜单并通知界面替换导航集合。</summary>
    public void ReloadNavigation()
    {
        NavigationItems = RouterConfiguration.Load();
        OnPropertyChanged(nameof(NavigationItems));
        foreach (var tab in OpenTabs) UpdateTabTitle(tab);
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
        // 只有导航成功后才更新菜单高亮，拒绝访问时保留原选择。
        // 菜单选中状态由实际嵌入区域的 Navigated 事件更新。
    }

    /// <summary>即时刷新各层级菜单的中英文名称。</summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var item in NavigationItems) item.RefreshLanguage();
        foreach (var tab in OpenTabs) UpdateTabTitle(tab);
    }
    /// <summary>通过统一导航入口切换到主页。</summary>
    [RelayCommand]
    private void ShowHome() => NavigateTo("home");

    /// <summary>通过统一导航入口切换到设置页。</summary>
    [RelayCommand]
    private void ShowSettings() => NavigateTo("settings");

    /// <summary>通过统一导航入口切换到主题配色页。</summary>
    [RelayCommand]
    private void ShowThemeColors() => NavigateTo("theme/colors");

    /// <summary>在中文与英文之间切换，并由语言管理器广播属性刷新。</summary>
    [RelayCommand]
    private void ChangeLanguage()
    {
        var language = LanguageManager.Instance.CurrentCulture == "zh" ? "en" : "zh";
        LanguageManager.Instance.ChangeLang(language);
    }
    /// <summary>按首次打开顺序维护页签，相同路由不会重复创建。</summary>
    public ObservableCollection<WorkspaceTab> OpenTabs { get; } = [];

    private void OnRouteFloated(object? sender, RouteNavigatedEventArgs args)
    {
        if (!string.Equals(args.RegionName, "MainRegion", StringComparison.OrdinalIgnoreCase)) return;
        var tab = OpenTabs.FirstOrDefault(x => string.Equals(x.Url, args.Url, StringComparison.OrdinalIgnoreCase));
        if (tab is null) return;
        var index = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);
        if (OpenTabs.Count > 0)
            NavigateTo(OpenTabs[Math.Min(index, OpenTabs.Count - 1)].Url);
        else
            foreach (var item in NavigationItems) item.SelectRoute("");
    }
    private void OnRouteNavigated(object? sender, RouteNavigatedEventArgs args)
    {
        if (!string.Equals(args.RegionName, "MainRegion", StringComparison.OrdinalIgnoreCase)) return;
        var tab = OpenTabs.FirstOrDefault(x => string.Equals(x.Url, args.Url, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            tab = new WorkspaceTab(args.Url);
            UpdateTabTitle(tab);
            OpenTabs.Add(tab);
        }
        foreach (var item in OpenTabs) item.IsSelected = ReferenceEquals(item, tab);
        foreach (var item in NavigationItems) item.SelectRoute(args.Url);
    }

    /// <summary>递归查找菜单标题，语言切换或路由配置更新后同步页签。</summary>
    internal NavModel? FindNavigationItem(string url)
    {
        NavModel? Find(IEnumerable<NavModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (string.Equals(node.Url?.Trim('/'), url.Trim('/'), StringComparison.OrdinalIgnoreCase)) return node;
                if (Find(node.Children) is { } match) return match;
            }
            return null;
        }
        return Find(NavigationItems);
    }

    private void UpdateTabTitle(WorkspaceTab tab)
    {
        var match = FindNavigationItem(tab.Url);
        tab.Title = match?.Title ?? tab.Url;
        tab.Icon = match?.Icon ?? PackIconKind.FileOutline;
    }

    /// <summary>通过导航服务激活缓存页面，保留编辑状态并再次校验权限。</summary>
    [RelayCommand]
    private void SelectTab(WorkspaceTab? tab)
    {
        if (tab is not null) NavigateTo(tab.Url);
    }

    /// <summary>关闭当前标签时先尝试激活相邻标签，最后一项关闭后清空内容区。</summary>
    [RelayCommand]
    private void CloseTab(WorkspaceTab? tab)
    {
        if (tab is null || !OpenTabs.Contains(tab)) return;
        var index = OpenTabs.IndexOf(tab);
        if (tab.IsSelected && OpenTabs.Count > 1)
        {
            var next = OpenTabs[index == OpenTabs.Count - 1 ? index - 1 : index + 1];
            NavigateTo(next.Url);
            if (!next.IsSelected) return;
        }
        if (_navigation is NavigationService navigation) navigation.ClosePage("MainRegion", tab.Url);
        OpenTabs.Remove(tab);
        if (OpenTabs.Count == 0)
            foreach (var item in NavigationItems) item.SelectRoute("");
    }
}

