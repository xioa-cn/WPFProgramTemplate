using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Region;

/// <summary>管理区域注册、页面激活和页面导航。</summary>
public sealed class RegionManager : IRegionNavigationService
{
    private static readonly ConditionalWeakTable<IServiceProvider, RegionManager> Instances = new();
    private readonly IServiceProvider _rootProvider;
    private readonly Dictionary<string, ContentControl> _regions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Stack<UIElement>> _history = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>区域名称附加属性。</summary>
    public static readonly DependencyProperty RegionNameProperty = DependencyProperty.RegisterAttached("RegionName", typeof(string), typeof(RegionManager), new PropertyMetadata(null, OnRegionNameChanged));
    public static void SetRegionName(DependencyObject element, string? value) => element.SetValue(RegionNameProperty, value);
    public static string? GetRegionName(DependencyObject element) => (string?)element.GetValue(RegionNameProperty);

    public RegionManager(IServiceProvider rootProvider) => _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));

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
    public void Clear(string regionName) => Get(regionName).Content = null;

    /// <summary>解析并导航到指定页面，页面的 DataContext 由 ModuleUI 自动组装。</summary>

    /// <summary>激活页面并执行完整导航生命周期。</summary>
    public UIElement Navigate(string regionName, UIElement view, bool keepAlive = true)
    {
        regionName = RegionRoute.Normalize(regionName);
        var host = Get(regionName);
        var old = host.Content as UIElement;
        if (old == view) return view;
        if (old is INavigationAware oldAware) oldAware.OnNavigatedFrom();
        if (old is IRegionView oldRegion) oldRegion.OnDeactivated();
        if (view is INavigationAware aware && !aware.IsNavigationTarget(new RegionNavigationContext(regionName))) return view;
        view.AssemblyUI();
        host.Content = view;
        if (!_history.TryGetValue(regionName, out var stack)) _history[regionName] = stack = new Stack<UIElement>();
        if (keepAlive || stack.Count == 0) stack.Push(view);
        if (view is IRegionView region) region.OnActivated();
        if (view is INavigationAware target) target.OnNavigatedTo(new RegionNavigationContext(regionName));
        return view;
    }

    /// <summary>返回上一个页面。</summary>
    public bool GoBack(string regionName)
    {
        regionName = RegionRoute.Normalize(regionName);
        if (!_history.TryGetValue(regionName, out var stack) || stack.Count < 2) return false;
        stack.Pop(); Navigate(regionName, stack.Peek(), true); return true;
    }

    public bool CanNavigate(string regionName) => _regions.ContainsKey(RegionRoute.Normalize(regionName));
    public ContentControl Get(string regionName) => _regions.TryGetValue(RegionRoute.Normalize(regionName), out var host) ? host : throw new KeyNotFoundException($"区域 '{regionName}' 未注册。");
}
