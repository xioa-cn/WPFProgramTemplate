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

        var result = _regionManager.Navigate(regionName, view, keepAlive);
        if (!keepAlive)
            _viewCache.Remove(cacheKey);
        return result;
    }

    public bool CanNavigate(string url) => _routes.ContainsKey(RegionRoute.Normalize(url));
    public bool GoBack(string regionName) => _regionManager.GoBack(regionName);
}

/// <summary>应用导航服务接口。</summary>
public interface INavigationService
{
    void Register(string url, Type viewType, string? moduleName = null);
    UIElement Navigate(string regionName, string url, bool keepAlive = true);
    bool CanNavigate(string url);
    bool GoBack(string regionName);
}
