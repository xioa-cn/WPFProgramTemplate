using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Region;

/// <summary>管理区域注册、页面激活和页面导航。</summary>
public sealed class RegionManager : IRegionNavigationService
{
    private static readonly ConditionalWeakTable<IServiceProvider, RegionManager> Instances = new();
    // Keep each cached Page attached to its own Frame when switching region content.
    private readonly ConditionalWeakTable<Page, Frame> _pageHosts = new();
    private readonly ConditionalWeakTable<UIElement, object> _assembledViews = new();
    private readonly Dictionary<UIElement, (Window Window, ContentControl Host)> _floatingViews = new();
    private readonly Dictionary<string, UIElement> _activeViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, HashSet<string>> _pagePermissions = new();
    private readonly IServiceProvider _rootProvider;

    /// <summary>登记页面类型的权限；同类型存在多个路由时要求全部授权，防止别名绕过。</summary>
    internal void RegisterPermission(Type type, string key)
    {
        if (!_pagePermissions.TryGetValue(type, out var keys)) _pagePermissions[type] = keys = new(StringComparer.OrdinalIgnoreCase);
        keys.Add(key);
    }

    /// <summary>检查页面实例，包括区域直接导航和历史返回。</summary>
    private void CheckPermission(UIElement view)
    {
        var permissions = _rootProvider.GetService<Mapper.PermissionService>();
        if (permissions is null) return;
        if (!_pagePermissions.TryGetValue(view.GetType(), out var keys))
            throw new UnauthorizedAccessException("页面未登记权限。");
        foreach (var key in keys) permissions.Demand(key);
    }

    /// <summary>
    /// 获取视图和其 DataContext 上的导航生命周期对象。
    /// 视图与 VM 同时实现时两者都会收到回调，但同一个实例只处理一次。
    /// </summary>
    private static IReadOnlyList<INavigationAware> GetNavigationAware(UIElement view)
    {
        var result = new List<INavigationAware>(capacity: 2);
        if (view is INavigationAware viewAware) result.Add(viewAware);
        if (view is FrameworkElement { DataContext: INavigationAware dataContextAware } &&
            !result.Contains(dataContextAware))
            result.Add(dataContextAware);
        return result;
    }
    private readonly Dictionary<string, ContentControl> _regions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Stack<UIElement>> _history = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>区域名称附加属性。</summary>
    public static readonly DependencyProperty RegionNameProperty = DependencyProperty.RegisterAttached("RegionName", typeof(string), typeof(RegionManager), new PropertyMetadata(null, OnRegionNameChanged));
    public static void SetRegionName(DependencyObject element, string? value) => element.SetValue(RegionNameProperty, value);
    public static string? GetRegionName(DependencyObject element) => (string?)element.GetValue(RegionNameProperty);

    /// <summary>身份或授权变化后清空可见内容及历史，防止继续操作旧身份页面。</summary>
    public RegionManager(IServiceProvider rootProvider)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        if (_rootProvider.GetService<Mapper.PermissionService>() is { } permissions)
            permissions.Changed += (_, _) =>
            {
                foreach (var view in _floatingViews.Keys.ToArray()) CloseFloatingView(view);
                foreach (var host in _regions.Values)
                    host.Dispatcher.Invoke(() => host.Content = null);
                _activeViews.Clear();
                _history.Clear();
            };
    }

    private static void OnRegionNameChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not ContentControl host || args.NewValue is not string name || string.IsNullOrWhiteSpace(name)) return;
        var provider = MainProvider.ServiceProvider ?? throw new InvalidOperationException("主服务容器尚未初始化。");
        var manager = provider.GetService<RegionManager>() ?? Instances.GetValue(provider, p => new RegionManager(p));
        manager.Register(name, host);
    }

    /// <summary>注册区域宿主。</summary>
    public void Register(string regionName, ContentControl host)
    {
        regionName = RegionRoute.Normalize(regionName);
        ArgumentNullException.ThrowIfNull(host);
        if (!_regions.TryAdd(regionName, host)) throw new InvalidOperationException($"区域 '{regionName}' 已注册。");
    }
    public void RegisterRegion(string regionName, ContentControl host) => Register(regionName, host);
    public ContentControl GetRegion(string regionName) => Get(regionName);
    public void Show(string regionName, UIElement view) => Navigate(regionName, view);
    public void Clear(string regionName)
    {
        regionName = RegionRoute.Normalize(regionName);
        Get(regionName).Content = null;
        _activeViews.Remove(regionName);
    }

    /// <summary>解析并导航到指定页面，页面的 DataContext 由 ModuleUI 自动组装。</summary>

    /// <summary>激活页面并执行完整导航生命周期。</summary>
    public UIElement Navigate(string regionName, UIElement view, bool keepAlive = true)
    {
        regionName = RegionRoute.Normalize(regionName);
        CheckPermission(view);
        if (_floatingViews.TryGetValue(view, out var floating))
        {
            if (floating.Window.WindowState == WindowState.Minimized) floating.Window.WindowState = WindowState.Normal;
            floating.Window.Activate();
            return view;
        }
        var host = Get(regionName);
        var old = _activeViews.GetValueOrDefault(regionName) ?? host.Content as UIElement;
        if (old == view) return view;
        var timing = Stopwatch.StartNew();
        // 先组装页面，才能解析由 ModuleUI 注入的 ViewModel 生命周期对象。
        if (!_assembledViews.TryGetValue(view, out _))
        {
            view.AssemblyUI();
            _assembledViews.Add(view, new object());
        }
        var aware = GetNavigationAware(view);
        var context = new RegionNavigationContext(regionName);
        if (aware.Any(candidate => !candidate.IsNavigationTarget(context))) return view;

        foreach (var oldAware in old is null ? [] : GetNavigationAware(old)) oldAware.OnNavigatedFrom();
        if (old is IRegionView oldRegion) oldRegion.OnDeactivated();

        var deactivateMs = timing.Elapsed.TotalMilliseconds;
        var assemblyMs = timing.Elapsed.TotalMilliseconds;
        host.Content = view is Page page
            ? _pageHosts.GetValue(page, CreatePageHost)
            : view;
        var contentMs = timing.Elapsed.TotalMilliseconds;
        _activeViews[regionName] = view;
        if (!_history.TryGetValue(regionName, out var stack)) _history[regionName] = stack = new Stack<UIElement>();
        if (keepAlive || stack.Count == 0) stack.Push(view);
        if (view is IRegionView region) region.OnActivated();
        foreach (var target in aware) target.OnNavigatedTo(context);
        return view;
    }

    /// <summary>关闭页面时移除宿主引用和历史记录，防止后退重新打开已关闭页签。</summary>
    public void RemoveView(string regionName, UIElement view)
    {
        regionName = RegionRoute.Normalize(regionName);
        CloseFloatingView(view);
        if (_activeViews.GetValueOrDefault(regionName) == view)
        {
            foreach (var aware in GetNavigationAware(view)) aware.OnNavigatedFrom();
            if (view is IRegionView regionView) regionView.OnDeactivated();
            Clear(regionName);
        }
        if (_history.TryGetValue(regionName, out var history))
            _history[regionName] = new Stack<UIElement>(history.Reverse().Where(item => !ReferenceEquals(item, view)));
    }

    /// <summary>返回上一个页面。</summary>
    public bool GoBack(string regionName)
    {
        regionName = RegionRoute.Normalize(regionName);
        if (!_history.TryGetValue(regionName, out var stack) || stack.Count < 2) return false;
        var current = stack.Pop();
        var previous = stack.Peek();
        try
        {
            Navigate(regionName, previous, false);
            if (_activeViews.GetValueOrDefault(regionName) == previous) return true;
            stack.Push(current);
            return false;
        }
        catch
        {
            stack.Push(current);
            throw;
        }
    }

    /// <summary>移动现有页面宿主到独立窗口，保留页面和 DataContext。</summary>
    public void FloatView(string regionName, UIElement view, Window window, ContentControl host, Action dock)
    {
        regionName = RegionRoute.Normalize(regionName);
        CheckPermission(view);
        if (_floatingViews.TryGetValue(view, out var existing))
        {
            if (existing.Window.WindowState == WindowState.Minimized) existing.Window.WindowState = WindowState.Normal;
            existing.Window.Activate();
            return;
        }
        var owner = window.Owner ?? throw new InvalidOperationException("弹出窗口必须指定主窗口。");
        var regionHost = Get(regionName);
        UIElement content = view is Page page ? _pageHosts.GetValue(page, CreatePageHost) : view;
        host.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        host.VerticalContentAlignment = VerticalAlignment.Stretch;
        // Page 连同原有 Frame 一起移动，避免重复建立父子关系或重建页面。
        RemoveView(regionName, view);
        // 清空 Content 后同步旧模板，释放 ContentPresenter 对页面/Frame 的引用。
        regionHost.UpdateLayout();
        try { host.Content = content; }
        catch
        {
            host.Content = null;
            host.UpdateLayout();
            dock();
            throw;
        }
        _floatingViews.Add(view, (window, host));
        var ownerClosing = false;
        System.ComponentModel.CancelEventHandler ownerClosingHandler = (_, _) =>
        {
            ownerClosing = true;
            owner.Dispatcher.BeginInvoke(new Action(() => ownerClosing = false));
        };
        owner.Closing += ownerClosingHandler;
        window.Closed += (_, _) =>
        {
            owner.Closing -= ownerClosingHandler;
            host.Content = null;
            host.UpdateLayout();
            if (!_floatingViews.Remove(view)) return;
            if (ownerClosing || owner.Dispatcher.HasShutdownStarted || !owner.IsVisible) return;
            try
            {
                foreach (var aware in GetNavigationAware(view)) aware.OnNavigatedFrom();
                if (view is IRegionView active) active.OnDeactivated();
                dock();
            }
            catch (Exception ex)
            {
                Machine.ModuleLoad.Logger.GlobalLogger.Error(ex.ToString());
                MessageBox.Show(owner, ex.Message, "页面返回失败");
            }
        };
        try
        {
            window.Show();
            if (view is IRegionView active) active.OnActivated();
            foreach (var aware in GetNavigationAware(view))
                aware.OnNavigatedTo(new RegionNavigationContext(regionName));
        }
        catch
        {
            CloseFloatingView(view);
            dock();
            throw;
        }
    }

    public bool IsFloating(UIElement view) => _floatingViews.ContainsKey(view);

    private void CloseFloatingView(UIElement view)
    {
        if (!_floatingViews.Remove(view, out var floating)) return;
        floating.Window.Dispatcher.Invoke(() =>
        {
            floating.Host.Content = null;
            floating.Host.UpdateLayout();
            floating.Window.Close();
        });
    }
    private static Frame CreatePageHost(Page page)
    {
        var frame = new Frame
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            NavigationUIVisibility = NavigationUIVisibility.Hidden,
            JournalOwnership = JournalOwnership.OwnsJournal
        };
        // Frame 是一个页面宿主，而不是第二个导航控制器。
        // 在设置 Content 之前订阅：初始导航是异步的，必须保持允许。
        // 拒绝 URI 链接、本地的后退/前进/刷新以及直接的 Navigate 调用，这些会绕过区域历史记录、视图缓存和导航生命周期回调。
        frame.Navigating += (_, args) =>
        {
            if (args.NavigationMode != NavigationMode.New || !ReferenceEquals(args.Content, page))
                args.Cancel = true;
        };
        frame.Content = page;
        return frame;
    }
    public bool CanNavigate(string regionName) => _regions.ContainsKey(RegionRoute.Normalize(regionName));
    public ContentControl Get(string regionName) => _regions.TryGetValue(RegionRoute.Normalize(regionName), out var host) ? host : throw new KeyNotFoundException($"区域 '{regionName}' 未注册。");
}
