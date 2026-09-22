using System.Diagnostics;
using System.Windows.Threading;
using System.Windows;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Region;

/// <summary>按 URL 注册和解析页面的导航服务。</summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _rootProvider;
    private readonly RegionManager _regionManager;
    private readonly Dictionary<string, (Type ViewType, string? ModuleName)> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UIElement> _viewCache = new(StringComparer.OrdinalIgnoreCase);

    public NavigationService(IServiceProvider rootProvider, RegionManager regionManager)
    {
        _rootProvider = rootProvider;
        _regionManager = regionManager;
        if (_rootProvider.GetService<Mapper.PermissionService>() is { } permissions)
            permissions.Changed += (_, _) => _viewCache.Clear();
    }

    /// <summary>成功激活路由后通知页签等导航观察者。</summary>
    public event EventHandler<RouteNavigatedEventArgs>? Navigated;
    public event EventHandler<RouteNavigatedEventArgs>? Floated;

    /// <summary>移除已关闭页签的缓存和历史，重新打开时创建新页面。</summary>
    public void ClosePage(string regionName, string url)
    {
        var key = $"{RegionRoute.Normalize(regionName)}::{RegionRoute.Normalize(url)}";
        if (_viewCache.Remove(key, out var view)) _regionManager.RemoveView(regionName, view);
    }

    /// <summary>注册 URL 到视图类型的映射。</summary>
    public void Register(string url, Type viewType, string? moduleName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!typeof(UIElement).IsAssignableFrom(viewType)) throw new ArgumentException("视图类型必须继承 UIElement。", nameof(viewType));
        var normalized = RegionRoute.Normalize(url);
        _routes[normalized] = (viewType, moduleName);
        _regionManager.RegisterPermission(viewType, "page:" + normalized);

    }

    /// <summary>按 URL 创建视图并导航到区域。</summary>
    public UIElement Navigate(string regionName, string url, bool keepAlive = true)
    {
        var timing = Stopwatch.StartNew();
        NavigationTimingLog.Write($"[NavigationTiming] begin region={regionName}, url={url}");
        var normalizedUrl = RegionRoute.Normalize(url);
        var normalizedRegion = RegionRoute.Normalize(regionName);
        if (!_routes.TryGetValue(normalizedUrl, out var route))
            throw new KeyNotFoundException($"未注册导航页面: {url}");
        // 缓存命中也必须重新检查身份，避免切换账号后复用越权页面。
        _rootProvider.GetService<Mapper.PermissionService>()?.Demand("page:" + normalizedUrl);
        var cacheKey = $"{normalizedRegion}::{normalizedUrl}";
        UIElement view;
        if (keepAlive && _viewCache.TryGetValue(cacheKey, out var cachedView))
        {
            view = cachedView;
        }
        else
        {
            var provider = string.IsNullOrWhiteSpace(route.ModuleName) ? _rootProvider : ModuleProvider.GetModuleProvider(route.ModuleName);
            view = (UIElement)provider.GetRequiredService(route.ViewType);
            if (keepAlive)
                _viewCache[cacheKey] = view;
        }

        var resolveMs = timing.Elapsed.TotalMilliseconds;
        var result = _regionManager.Navigate(regionName, view, keepAlive);
        var navigateMs = timing.Elapsed.TotalMilliseconds - resolveMs;
        NavigationTimingLog.Write($"[NavigationTiming] {url}: resolve={resolveMs:F1}ms, navigate={navigateMs:F1}ms");
        // Include deferred layout, Loaded/Unloaded handlers and pending binding work.
        view.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            NavigationTimingLog.Write($"[NavigationTiming] {url}: dispatcher-idle={timing.Elapsed.TotalMilliseconds:F1}ms")));
        if (!keepAlive)
            _viewCache.Remove(cacheKey);
        if (ReferenceEquals(result, view) && !_regionManager.IsFloating(view))
            Navigated?.Invoke(this, new RouteNavigatedEventArgs(normalizedRegion, normalizedUrl));
        return result;
    }

    /// <summary>弹出缓存页面；关闭独立窗口时重新通过路由返回。</summary>
    public void FloatPage(string regionName, string url, Window window, System.Windows.Controls.ContentControl host)
    {
        var view = Navigate(regionName, url);
        if (_regionManager.IsFloating(view)) return;
        _regionManager.FloatView(regionName, view, window, host, () => Navigate(regionName, url));
        if (!_regionManager.IsFloating(view)) return;
        Floated?.Invoke(this, new RouteNavigatedEventArgs(RegionRoute.Normalize(regionName), RegionRoute.Normalize(url)));
        window.Activate();
    }
    public IReadOnlyList<RegisteredRoute> GetRegisteredRoutes() => _routes.Select(route => new RegisteredRoute(route.Key, route.Value.ViewType, route.Value.ModuleName)).OrderBy(route => route.Url).ToArray();

    public bool CanNavigate(string url) => _routes.ContainsKey(RegionRoute.Normalize(url)) && (_rootProvider.GetService<Mapper.PermissionService>()?.Allows("page:" + RegionRoute.Normalize(url)) ?? true);
    public bool GoBack(string regionName) => _regionManager.GoBack(regionName);
}

/// <summary>应用导航服务接口。</summary>
public interface INavigationService
{
    void Register(string url, Type viewType, string? moduleName = null);
    UIElement Navigate(string regionName, string url, bool keepAlive = true);
    IReadOnlyList<RegisteredRoute> GetRegisteredRoutes() => Array.Empty<RegisteredRoute>();
    bool CanNavigate(string url);
    bool GoBack(string regionName);
}

public sealed record RegisteredRoute(string Url, Type ViewType, string? ModuleName);

/// <summary>导航完成后的区域和路由信息。</summary>
public sealed class RouteNavigatedEventArgs(string regionName, string url) : EventArgs
{
    public string RegionName { get; } = regionName;
    public string Url { get; } = url;
}