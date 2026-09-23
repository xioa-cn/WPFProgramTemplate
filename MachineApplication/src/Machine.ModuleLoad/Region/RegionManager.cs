using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Region;

/// <summary>管理 ContentControl 区域、视图发现及区域导航；保留原有实例导航入口。</summary>
public sealed class RegionManager : IRegionManager, IDisposable
{
    private static readonly ConditionalWeakTable<IServiceProvider, RegionManager> Instances = new();
    private readonly ConditionalWeakTable<Page, Frame> _pageHosts = new();
    private readonly ConditionalWeakTable<UIElement, object> _assembledViews = new();
    private readonly Dictionary<UIElement, (Window Window, ContentControl Host)> _floatingViews = new();
    private readonly Dictionary<string, RegionInfo> _regions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, HashSet<string>> _pagePermissions;
    private readonly Dictionary<string, List<ViewRegistration>> _viewRegistrations;
    private readonly IServiceProvider _rootProvider;
    private readonly RegionManager? _parent;
    private readonly Mapper.PermissionService? _permissions;
    private NavigationService? _catalog;
    internal NavigationService? Catalog => _catalog ?? _parent?.Catalog ?? _rootProvider.GetService<NavigationService>();

    public RegionManager(IServiceProvider rootProvider) : this(rootProvider, null) { }
    private RegionManager(IServiceProvider rootProvider, RegionManager? parent)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        _parent = parent;
        _pagePermissions = parent?._pagePermissions ?? new();
        _viewRegistrations = parent?._viewRegistrations ?? new(StringComparer.OrdinalIgnoreCase);
        Regions = new RegionCollection(this);
        _permissions = rootProvider.GetService<Mapper.PermissionService>();
        if (_permissions is not null) _permissions.Changed += OnPermissionsChanged;
    }

    internal void AttachCatalog(NavigationService catalog) => _catalog = catalog;
    public IRegionCollection Regions { get; }

    internal void RegisterPermission(Type type, string key)
    {
        if (!_pagePermissions.TryGetValue(type, out var keys))
            _pagePermissions[type] = keys = new(StringComparer.OrdinalIgnoreCase);
        keys.Add(key);
    }

    internal void CheckPermission(UIElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (_permissions is null) return;
        if (!_pagePermissions.TryGetValue(view.GetType(), out var keys))
            throw new UnauthorizedAccessException("页面未登记权限。");
        foreach (var key in keys) _permissions.Demand(key);
    }

    internal void Assemble(UIElement view)
    {
        if (_assembledViews.TryGetValue(view, out _)) return;
        view.AssemblyUI();
        _assembledViews.Add(view, new object());
    }

    internal static IReadOnlyList<INavigationAware> NavigationAware(UIElement view)
    {
        var result = new List<INavigationAware>(2);
        if (view is INavigationAware aware) result.Add(aware);
        if (view is FrameworkElement { DataContext: INavigationAware model } &&
            !result.Any(item => ReferenceEquals(item, model))) result.Add(model);
        return result;
    }

    public static readonly DependencyProperty RegionManagerProperty = DependencyProperty.RegisterAttached(
        "RegionManager", typeof(IRegionManager), typeof(RegionManager),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
    public static void SetRegionManager(DependencyObject element, IRegionManager value) => element.SetValue(RegionManagerProperty, value);
    public static IRegionManager? GetRegionManager(DependencyObject element) => (IRegionManager?)element.GetValue(RegionManagerProperty);
    public static readonly DependencyProperty RegionNameProperty = DependencyProperty.RegisterAttached(
        "RegionName", typeof(string), typeof(RegionManager), new PropertyMetadata(null, OnRegionNameChanged));
    public static void SetRegionName(DependencyObject element, string? value) => element.SetValue(RegionNameProperty, value);
    public static string? GetRegionName(DependencyObject element) => (string?)element.GetValue(RegionNameProperty);

    private static void OnRegionNameChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not ContentControl host || args.NewValue is not string name || string.IsNullOrWhiteSpace(name)) return;
        var manager = GetRegionManager(host);
        if (manager is null)
        {
            var provider = MainProvider.ServiceProvider ?? throw new InvalidOperationException("主服务容器尚未初始化。");
            manager = provider.GetService<IRegionManager>() ?? provider.GetService<RegionManager>()
                ?? Instances.GetValue(provider, p => new RegionManager(p));
        }
        if (args.OldValue is string oldName && manager.Regions.ContainsRegionWithName(oldName) &&
            ReferenceEquals(manager.Regions[oldName].Host, host)) manager.Regions.Remove(oldName);
        manager.Register(name, host);
    }

    public void Register(string regionName, ContentControl host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.Dispatcher.VerifyAccess();
        regionName = RegionRoute.Normalize(regionName);
        var region = new RegionInfo(this, regionName, host);
        if (!_regions.TryAdd(regionName, region)) throw new InvalidOperationException($"区域 '{regionName}' 已注册。");
        region.Service.Navigated += (_, args) => Catalog?.OnRegionNavigated(this, region, args.NavigationContext);
        if (host.Content is UIElement initial) region.Add(initial);
        if (_viewRegistrations.TryGetValue(regionName, out var registrations))
            foreach (var registration in registrations) region.Add(registration.Factory());
    }

    public void RegisterRegion(string regionName, ContentControl host) => Register(regionName, host);
    public ContentControl GetRegion(string regionName) => Get(regionName);
    IRegion IRegionManager.GetRegion(string regionName) => GetRegionInfo(regionName);
    public ContentControl Get(string regionName) => GetRegionInfo(regionName).Host;
    internal RegionInfo GetRegionInfo(string name) => _regions.TryGetValue(RegionRoute.Normalize(name), out var region)
        ? region : throw new KeyNotFoundException($"区域 '{name}' 未注册。");
    public bool CanNavigate(string regionName) => _regions.ContainsKey(RegionRoute.Normalize(regionName));
    public void Show(string regionName, UIElement view) => Navigate(regionName, view);

    public IRegionManager AddToRegion(string regionName, UIElement view)
    {
        GetRegionInfo(regionName).Add(view);
        return this;
    }

    public IRegionManager RegisterViewWithRegion(string regionName, Type viewType)
    {
        ArgumentNullException.ThrowIfNull(viewType);
        if (!typeof(UIElement).IsAssignableFrom(viewType)) throw new ArgumentException("视图必须继承 UIElement。", nameof(viewType));
        RegisterPermission(viewType, "page:" + viewType.Name);
        return RegisterDiscovery(regionName, new(viewType, () => CreateView(viewType)));
    }

    public IRegionManager RegisterViewWithRegion(string regionName, Func<UIElement> viewFactory)
    {
        ArgumentNullException.ThrowIfNull(viewFactory);
        return RegisterDiscovery(regionName, new(null, viewFactory));
    }

    private IRegionManager RegisterDiscovery(string regionName, ViewRegistration registration)
    {
        regionName = RegionRoute.Normalize(regionName);
        if (!_viewRegistrations.TryGetValue(regionName, out var registrations))
            _viewRegistrations[regionName] = registrations = [];
        registrations.Add(registration);
        if (_regions.TryGetValue(regionName, out var region)) region.Add(registration.Factory());
        return this;
    }

    public IRegionManager CreateRegionManager() => new RegionManager(_rootProvider, this);

    public void RequestNavigate(string regionName, Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!_regions.TryGetValue(RegionRoute.Normalize(regionName), out var region))
        {
            callback?.Invoke(new(null, false, new KeyNotFoundException($"区域 '{regionName}' 未注册。")));
            return;
        }
        region.Service.RequestNavigate(target, callback, navigationParameters);
    }
    public void RequestNavigate(string regionName, Uri target, NavigationParameters parameters,
        Action<NavigationResult>? callback = null) => RequestNavigate(regionName, target, callback, parameters);

    internal UIElement CreateView(Type type) => (UIElement)ActivatorUtilities.GetServiceOrCreateInstance(_rootProvider, type);

    internal UIElement ResolveContent(RegionInfo region, RegionNavigationContext context, bool reuse = true)
    {
        var path = RegionRoute.GetPath(context.Uri);
        Type? type = null;
        Func<UIElement>? factory = null;
        if (Catalog?.TryGetRoute(path, out var route) == true)
        {
            type = route.ViewType;
            factory = route.Factory;
            _permissions?.Demand("page:" + path);
        }
        else if (_viewRegistrations.TryGetValue(region.Name, out var registrations))
        {
            var registered = registrations.FirstOrDefault(x => x.Type?.Name == path || x.Type?.FullName == path);
            type = registered?.Type;
            factory = registered?.Factory;
        }

        bool Matches(UIElement view) => region.IsRoute(view, path) || ReferenceEquals(region.GetView(path), view) ||
            (type is not null ? view.GetType() == type : view.GetType().Name == path || view.GetType().FullName == path);
        if (reuse)
        {
            var floating = _floatingViews.Keys.FirstOrDefault(view => type is not null && view.GetType() == type);
            if (floating is not null) { CheckPermission(floating); return floating; }
            foreach (var candidate in region.Views.Where(Matches).ToArray())
            {
                Assemble(candidate);
                if (!NavigationAware(candidate).All(item => item.IsNavigationTarget(context))) continue;
                CheckPermission(candidate);
                return candidate;
            }
        }
        if (factory is null) throw new KeyNotFoundException($"未注册导航目标 '{context.Uri}'。");
        var view = factory();
        Assemble(view);
        CheckPermission(view);
        if (region.Contains(view))
            throw new InvalidOperationException("导航需要新视图，但容器返回了现有实例。请将该视图注册为 Transient。");
        return view;
    }

    public UIElement Navigate(string regionName, UIElement view, bool keepAlive = true)
    {
        var region = GetRegionInfo(regionName);
        if (ReferenceEquals(region.ActiveView, view))
        {
            CheckPermission(view);
            region.Service.CancelPending();
            return view;
        }
        return region.Service.NavigateResolved(view, region.GetUri(view), null, keepAlive, true);
    }

    public bool GoBack(string regionName) => GetRegionInfo(regionName).Service.History.Move(false);
    public bool GoForward(string regionName) => GetRegionInfo(regionName).Service.History.Move(true);

    public void Clear(string regionName)
    {
        var region = GetRegionInfo(regionName);
        region.Host.Dispatcher.VerifyAccess();
        region.Service.CancelPending();
        if (region.ActiveView is { } old)
        {
            foreach (var aware in NavigationAware(old)) aware.OnNavigatedFrom();
            region.SwitchTo(null);
            region.ReleaseIfNeeded(old);
        }
        region.Service.ClearCurrent();
        region.Service.History.Clear();
    }

    public void RemoveView(string regionName, UIElement view)
    {
        var region = GetRegionInfo(regionName);
        region.Host.Dispatcher.VerifyAccess();
        region.Service.CancelPending();
        var uri = region.Contains(view) ? region.GetUri(view) : null;
        CloseFloatingView(view);
        if (ReferenceEquals(region.ActiveView, view))
        {
            foreach (var aware in NavigationAware(view)) aware.OnNavigatedFrom();
            region.SwitchTo(null);
            region.Service.ClearCurrent();
        }
        region.RemoveCore(view);
        region.Service.History.RemoveView(view, uri);
    }

    private bool Unregister(string name)
    {
        if (!_regions.TryGetValue(RegionRoute.Normalize(name), out var region)) return false;
        Clear(name);
        region.RemoveAll();
        return _regions.Remove(region.Name);
    }

    private void OnPermissionsChanged(object? sender, EventArgs args)
    {
        foreach (var view in _floatingViews.Keys.ToArray()) CloseFloatingView(view);
        foreach (var region in _regions.Values.ToArray())
            region.Host.Dispatcher.Invoke(() =>
            {
                Clear(region.Name);
                foreach (var view in region.Views.ToArray()) region.RemoveCore(view);
            });
    }

    public void Dispose()
    {
        if (_permissions is not null) _permissions.Changed -= OnPermissionsChanged;
        foreach (var region in _regions.Values.ToArray()) region.Host.Dispatcher.Invoke(() => Unregister(region.Name));
    }

    internal UIElement GetPresentation(UIElement view) => view is Page page ? _pageHosts.GetValue(page, CreatePageHost) : view;
    internal bool FocusFloating(UIElement view)
    {
        if (!_floatingViews.TryGetValue(view, out var floating)) return false;
        if (floating.Window.WindowState == WindowState.Minimized) floating.Window.WindowState = WindowState.Normal;
        floating.Window.Activate();
        return true;
    }

    private sealed record ViewRegistration(Type? Type, Func<UIElement> Factory);
    private sealed class RegionCollection(RegionManager owner) : IRegionCollection
    {
        public int Count => owner._regions.Count;
        public IRegion this[string regionName] => owner.GetRegionInfo(regionName);
        public bool ContainsRegionWithName(string name) => owner.CanNavigate(name);
        public bool Remove(string name) => owner.Unregister(name);
        public IEnumerator<IRegion> GetEnumerator() => owner._regions.Values.Cast<IRegion>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
        var regionInfo = GetRegionInfo(regionName);
        var floatingContext = regionInfo.Service.CurrentContext ?? new RegionNavigationContext(regionInfo.NavigationService, regionInfo.GetUri(view));
        var regionHost = regionInfo.Host;
        UIElement content = view is Page page ? _pageHosts.GetValue(page, CreatePageHost) : view;
        host.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        host.VerticalContentAlignment = VerticalAlignment.Stretch;
        // Page 连同原有 Frame 一起移动，避免重复建立父子关系或重建页面。
        RemoveView(regionName, view);
        // 清空 Content 后同步旧模板，释放 ContentPresenter 对页面/Frame 的引用。
        regionHost.UpdateLayout();
        try
        {
            host.Content = content;
        }
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
                foreach (var aware in NavigationAware(view)) aware.OnNavigatedFrom();
                if (view is IRegionView active) active.OnDeactivated();
                regionInfo.AddCore(view);
                regionInfo.Remember(view, floatingContext.Uri, true);
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
            foreach (var aware in NavigationAware(view))
                aware.OnNavigatedTo(floatingContext);
        }
        catch
        {
            CloseFloatingView(view);
            dock();
            throw;
        }
    }

    public bool IsFloating(UIElement view) => _floatingViews.ContainsKey(view);

    internal void CloseFloatingPage(Type viewType)
    {
        foreach (var view in _floatingViews.Keys.Where(view => view.GetType() == viewType).ToArray())
            CloseFloatingView(view);
    }

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

}
