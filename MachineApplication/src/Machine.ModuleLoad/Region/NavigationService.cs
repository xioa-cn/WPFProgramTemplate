using System.Windows;
using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Region;

/// <summary>共享 URL 注册表；实际导航与实例保留由目标区域的服务管理。</summary>
public sealed class NavigationService : INavigationService, INavigateAsync
{
    private readonly IServiceProvider _provider;
    private readonly RegionManager _manager;
    private readonly Dictionary<string, RouteRegistration> _routes = new(StringComparer.OrdinalIgnoreCase);

    public NavigationService(IServiceProvider rootProvider, RegionManager regionManager)
    {
        _provider = rootProvider;
        _manager = regionManager;
        _manager.AttachCatalog(this);
    }

    public event EventHandler<RouteNavigatedEventArgs>? Navigated;
    public event EventHandler<RouteNavigatedEventArgs>? Floated;

    public void Register(string url, Type viewType, string? moduleName = null)
    {
        ArgumentNullException.ThrowIfNull(viewType);
        if (!typeof(UIElement).IsAssignableFrom(viewType)) throw new ArgumentException("视图必须继承 UIElement。", nameof(viewType));
        var path = RegionRoute.GetPath(new Uri(url, UriKind.RelativeOrAbsolute));
        _routes[path] = new(viewType, moduleName, () =>
        {
            var provider = string.IsNullOrWhiteSpace(moduleName) ? _provider : ModuleProvider.GetModuleProvider(moduleName);
            return (UIElement)provider.GetRequiredService(viewType);
        });
        _manager.RegisterPermission(viewType, "page:" + path);
    }

    internal bool TryGetRoute(string path, out RouteRegistration route) => _routes.TryGetValue(path, out route!);

    public UIElement Navigate(string regionName, string url, bool keepAlive = true)
        => Navigate(regionName, url, new NavigationParameters(), keepAlive);

    public UIElement Navigate(string regionName, string url, NavigationParameters parameters, bool keepAlive = true)
    {
        var region = _manager.GetRegionInfo(regionName);
        region.Host.Dispatcher.VerifyAccess();
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        var context = new RegionNavigationContext(region.NavigationService, uri, parameters);
        // keepAlive 只控制离开后的成员生命周期，不改变 URI 导航的实例复用规则。
        var view = _manager.ResolveContent(region, context, reuse: true);
        return region.Service.NavigateResolved(view, uri, parameters, keepAlive, false);
    }

    public void RequestNavigate(string regionName, Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
        => _manager.RequestNavigate(regionName, target, callback, navigationParameters);

    public void RequestNavigate(string regionName, Uri target, NavigationParameters parameters,
        Action<NavigationResult>? callback = null) => RequestNavigate(regionName, target, callback, parameters);

    public void RequestNavigate(string regionName, string target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
        => RequestNavigate(regionName, new Uri(target, UriKind.RelativeOrAbsolute), callback, navigationParameters);

    public void RequestNavigate(string regionName, string target, NavigationParameters parameters,
        Action<NavigationResult>? callback = null) => RequestNavigate(regionName, target, callback, parameters);

    public void RequestNavigate(Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
    {
        var name = _manager.Regions.ContainsRegionWithName("MainRegion") ? "MainRegion" :
            _manager.Regions.Count == 1 ? _manager.Regions.Single().Name : null;
        if (name is null)
        {
            callback?.Invoke(new(null, false, new InvalidOperationException("请指定要导航的区域名称。")));
            return;
        }
        RequestNavigate(name, target, callback, navigationParameters);
    }

    public void RequestNavigate(Uri target, NavigationParameters parameters, Action<NavigationResult>? callback = null)
        => RequestNavigate(target, callback, parameters);

    internal void OnRegionNavigated(RegionManager manager, RegionInfo region, RegionNavigationContext context)
    {
        if (!ReferenceEquals(manager, _manager) || !ReferenceEquals(region.Service.CurrentContext, context)) return;
        var path = RegionRoute.GetPath(context.Uri);
        if (_routes.ContainsKey(path)) Navigated?.Invoke(this, new(region.Name, path));
    }

    public void ClosePage(string regionName, string url)
    {
        var region = _manager.GetRegionInfo(regionName);
        var path = RegionRoute.GetPath(new Uri(url, UriKind.RelativeOrAbsolute));
        foreach (var view in region.Views.Where(view => region.IsRoute(view, path)).ToArray())
            _manager.RemoveView(regionName, view);
        region.Service.History.RemoveRoute(path);
        if (_routes.TryGetValue(path, out var registration))
            _manager.CloseFloatingPage(registration.ViewType);
    }

    /// <summary>确认成功后才将页面移入浮动窗口。</summary>
    public void FloatPage(string regionName, string url, Window window, ContentControl host)
    {
        RequestNavigate(regionName, new Uri(url, UriKind.RelativeOrAbsolute), result =>
        {
            if (!result.Result) return;
            var region = _manager.GetRegionInfo(regionName);
            if (region.Service.CurrentContext != result.Context || region.ActiveView is not { } view) return;
            _manager.FloatView(regionName, view, window, host,
                () => RequestNavigate(regionName, result.Context!.Uri, navigationParameters: result.Context.Parameters));
            if (!_manager.IsFloating(view)) return;
            Floated?.Invoke(this, new(region.Name, RegionRoute.GetPath(result.Context!.Uri)));
            window.Activate();
        });
    }

    public IReadOnlyList<RegisteredRoute> GetRegisteredRoutes() => _routes
        .Select(x => new RegisteredRoute(x.Key, x.Value.ViewType, x.Value.ModuleName)).OrderBy(x => x.Url).ToArray();
    public bool CanNavigate(string url)
    {
        var path = RegionRoute.GetPath(new Uri(url, UriKind.RelativeOrAbsolute));
        return _routes.ContainsKey(path) && (_provider.GetService<Mapper.PermissionService>()?.Allows("page:" + path) ?? true);
    }
    public bool GoBack(string regionName) => _manager.GoBack(regionName);
    public bool GoForward(string regionName) => _manager.GoForward(regionName);

    internal sealed record RouteRegistration(Type ViewType, string? ModuleName, Func<UIElement> Factory);
}

/// <summary>兼容原有应用导航入口。</summary>
public interface INavigationService
{
    void Register(string url, Type viewType, string? moduleName = null);
    UIElement Navigate(string regionName, string url, bool keepAlive = true);
    IReadOnlyList<RegisteredRoute> GetRegisteredRoutes() => Array.Empty<RegisteredRoute>();
    bool CanNavigate(string url);
    bool GoBack(string regionName);
}

public sealed record RegisteredRoute(string Url, Type ViewType, string? ModuleName);
public sealed class RouteNavigatedEventArgs(string regionName, string url) : EventArgs
{
    public string RegionName { get; } = regionName;
    public string Url { get; } = url;
}
