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
    }

    /// <summary>注册 URL 到视图类型的映射。</summary>
    public void Register(string url, Type viewType, string? moduleName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!typeof(UIElement).IsAssignableFrom(viewType)) throw new ArgumentException("视图类型必须继承 UIElement。", nameof(viewType));
        _routes[RegionRoute.Normalize(url)] = (viewType, moduleName);
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
        return result;
    }

    public IReadOnlyList<RegisteredRoute> GetRegisteredRoutes() => _routes.Select(route => new RegisteredRoute(route.Key, route.Value.ViewType, route.Value.ModuleName)).OrderBy(route => route.Url).ToArray();

    public bool CanNavigate(string url) => _routes.ContainsKey(RegionRoute.Normalize(url));
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
