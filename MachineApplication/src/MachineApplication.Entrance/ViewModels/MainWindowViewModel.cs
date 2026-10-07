using System.Collections.ObjectModel;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Region;
using CommunityToolkit.Mvvm.Input;
using I18nExtensions;
using MachineApplication.Entrance.Models;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Windows.Threading;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Utils;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>主窗口视图模型，负责菜单加载、区域导航与语言切换。</summary>
public partial class MainWindowViewModel : MachineViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly Dispatcher _dispatcher;
    private readonly HashSet<WorkspaceTab> _closingTabs = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isNavigationCollapsed;

    [RelayCommand]
    private void ToggleNavigation() => IsNavigationCollapsed = !IsNavigationCollapsed;

    /// <summary>底部作者、版权或联系信息，可按项目需要修改。</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _footerText = "© 2026 MachineApplication. Designed & Developed by XIOA";
    /// <summary>菜单以树结构配置，可在 Children 中继续增加层级。</summary>
    public MainWindowViewModel(INavigationService navigation, PermissionService permissions)
    {
        _navigation = navigation;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        ReloadNavigation();
        if (navigation is NavigationService routes)
        {
            routes.Navigated += OnRouteNavigated;
            routes.Floated += OnRouteFloated;
        }
        permissions.Changed += OnPermissionsChanged;
        permissions.CatalogChanged += (_, _) => ReloadNavigation();
        // 弱订阅避免语言服务一直持有已释放模块的 VM。
        PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
        RefreshLanguages();
    }
    public IReadOnlyList<NavModel> NavigationItems { get; private set; } = [];

    /// <summary>供标题栏下拉菜单绑定的实际可用语言列表。</summary>
    public IReadOnlyList<LanguageOption> Languages { get; private set; } = [];

    /// <summary>打开菜单时重新查询，兼容后续加载模块或增加语言资源，不写死语言数量。</summary>
    public void RefreshLanguages()
    {
        var manager = LanguageManager.Instance;
        Languages = manager.GetAvailableCultures()
            .Select(culture => new LanguageOption(culture,
                string.Equals(culture, manager.CurrentCulture, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        OnPropertyChanged(nameof(Languages));
    }

    /// <summary>按真实导航权限过滤菜单，移除无权页签，并保留当前页面的选中状态。</summary>
    public void ReloadNavigation()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ReloadNavigation);
            return;
        }

        try
        {
            NavigationItems = FilterNavigation(RouterConfiguration.Load());
        }
        catch (Exception exception)
        {
            NavigationItems = [];
            GlobalLogger.Error("读取导航菜单失败。", exception);
        }
        OnPropertyChanged(nameof(NavigationItems));

        foreach (var tab in OpenTabs.Where(tab => !CanNavigate(tab.Url)).ToArray())
        {
            if (_navigation is NavigationService navigation) navigation.ClosePage("MainRegion", tab.Url);
            OpenTabs.Remove(tab);
        }
        foreach (var tab in OpenTabs) UpdateTabTitle(tab);
        var selectedUrl = OpenTabs.FirstOrDefault(tab => tab.IsSelected)?.Url ?? "";
        foreach (var item in NavigationItems) item.SelectRoute(selectedUrl);
    }

    /// <summary>只过滤展示树，不修改 Router.json；空分组一并移除，保留顺序和多语言标题。</summary>
    private IReadOnlyList<NavModel> FilterNavigation(IEnumerable<NavModel> nodes)
    {
        var visible = new List<NavModel>();
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                var children = FilterNavigation(node.Children);
                if (children.Count == 0) continue;
                visible.Add(new NavModel(node.LanguageKey, node.Icon, node.Url, children.ToArray())
                {
                    Titles = node.Titles,
                    RequiredLevelIds = node.RequiredLevelIds,
                    IsExpanded = node.IsExpanded
                });
            }
            else if (node.Url is { } url && CanNavigate(url))
            {
                visible.Add(node);
            }
        }
        return visible;
    }

    /// <summary>菜单与快捷入口共用导航服务的权限规则，配置读取异常时按无权处理。</summary>
    private bool CanNavigate(string url)
    {
        try { return _navigation.CanNavigate(url); }
        catch (Exception exception)
        {
            GlobalLogger.Error($"检查页面导航权限失败：{url}", exception);
            return false;
        }
    }

    /// <summary>账号变化后丢弃旧账号的页签，并重新生成该账号可见的菜单。</summary>
    private void OnPermissionsChanged(object? sender, EventArgs args)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => OnPermissionsChanged(sender, args));
            return;
        }
        OpenTabs.Clear();
        ReloadNavigation();
    }

    /// <summary>分组按钮展开子菜单，页面按钮通过 URL 导航。</summary>
    [RelayCommand]
    private void ActivateNavigation(NavModel? item)
    {
        if (item is null) return;
        if (item.HasChildren) { if (!IsNavigationCollapsed) item.IsExpanded = !item.IsExpanded; }
        else if (item.Url is not null) NavigateTo(item.Url);
    }

    /// <summary>导航成功后更新整棵菜单，快捷入口也复用此方法。</summary>
    private void NavigateTo(string url, Action<bool>? completed = null)
    {
        if (!CanNavigate(url))
        {
            ReloadNavigation();
            completed?.Invoke(false);
            return;
        }

        _navigation.RequestNavigate("MainRegion", new Uri(url, UriKind.RelativeOrAbsolute), result =>
        {
            if (result.Error is UnauthorizedAccessException)
                ReloadNavigation();
            else if (result.Error is { } error)
            {
                GlobalLogger.Error($"页面导航失败：{url}", error);
                Growl.Error(error.Message);
            }
            completed?.Invoke(result.Result);
        });
        // 只有导航成功后才更新菜单高亮，拒绝访问时保留原选择。
        // 菜单选中状态由实际嵌入区域的 Navigated 事件更新。
    }

    /// <summary>即时刷新各层级菜单的中英文名称。</summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshLanguages();
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

    /// <summary>切换到菜单指定的已注册语言，由语言管理器广播界面刷新；重复选择不切换。</summary>
    [RelayCommand]
    private void ChangeLanguage(string? culture)
    {
        var manager = LanguageManager.Instance;
        if (string.IsNullOrWhiteSpace(culture) ||
            string.Equals(culture, manager.CurrentCulture, StringComparison.OrdinalIgnoreCase)) return;
        var language = manager.GetAvailableCultures()
            .FirstOrDefault(item => string.Equals(item, culture, StringComparison.OrdinalIgnoreCase));
        if (language is not null) manager.ChangeLang(language);
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

    /// <summary>关闭当前标签时等待相邻页面导航成功；失败或取消时保留原标签。</summary>
    [RelayCommand]
    private void CloseTab(WorkspaceTab? tab)
    {
        if (tab is null || !OpenTabs.Contains(tab) || _closingTabs.Contains(tab)) return;
        var index = OpenTabs.IndexOf(tab);
        if (tab.IsSelected && OpenTabs.Count > 1)
        {
            var next = OpenTabs[index == OpenTabs.Count - 1 ? index - 1 : index + 1];
            _closingTabs.Add(tab);
            try
            {
                // 导航包含异步加载和遮挡动画，不能在发起请求后立即检查选中状态。
                NavigateTo(next.Url, succeeded =>
                {
                    try
                    {
                        if (succeeded) RemoveTab(tab);
                    }
                    finally { _closingTabs.Remove(tab); }
                });
            }
            catch
            {
                _closingTabs.Remove(tab);
                throw;
            }
            return;
        }
        RemoveTab(tab);
    }

    /// <summary>同时移除页面缓存和页签；异步等待期间已被其他流程移除的页签不重复处理。</summary>
    private void RemoveTab(WorkspaceTab tab)
    {
        if (!OpenTabs.Contains(tab)) return;
        if (_navigation is NavigationService navigation) navigation.ClosePage("MainRegion", tab.Url);
        OpenTabs.Remove(tab);
        if (OpenTabs.Count == 0)
            foreach (var item in NavigationItems) item.SelectRoute("");
    }
}

