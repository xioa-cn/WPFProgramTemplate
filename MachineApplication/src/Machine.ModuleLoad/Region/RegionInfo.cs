using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Machine.ModuleLoad.Region;

/// <summary>由 RegionManager 注册的 ContentControl 单激活区域。</summary>
public sealed class RegionInfo : IRegion
{
    private readonly RegionManager _manager;
    private readonly ObservableCollection<UIElement> _views = [];
    private readonly ObservableCollection<UIElement> _activeViews = [];
    private readonly Dictionary<string, UIElement> _namedViews = new(StringComparer.Ordinal);
    private readonly Dictionary<UIElement, (Uri Uri, bool KeepAlive)> _metadata = new(ReferenceEqualityComparer.Instance);
    private IRegionAnimation _animation = new RegionAnimation();

    internal RegionInfo(RegionManager manager, string name, ContentControl host, IRegionAnimation? animation = null)
    {
        _manager = manager;
        Name = name;
        Host = host;
        Views = new(_views);
        ActiveViews = new(_activeViews);
        NavigationService = new RegionNavigationService(manager, this);
        Animation = animation ?? new RegionAnimation();
    }

    public string Name { get; }
    public ContentControl Host { get; }
    public object? Context { get; set; }
    public IRegionManager RegionManager => _manager;
    public IRegionNavigationService NavigationService { get; }
    public IRegionAnimation Animation
    {
        get => _animation;
        set => _animation = value ?? throw new ArgumentNullException(nameof(value));
    }
    internal RegionNavigationService Service => (RegionNavigationService)NavigationService;
    public ReadOnlyObservableCollection<UIElement> Views { get; }
    public ReadOnlyObservableCollection<UIElement> ActiveViews { get; }
    internal UIElement? ActiveView => _activeViews.FirstOrDefault();
    public bool Contains(UIElement view) => _views.Any(item => ReferenceEquals(item, view));
    public UIElement? GetView(string viewName) => _namedViews.GetValueOrDefault(viewName);

    public void Add(UIElement view, string? viewName = null)
    {
        Host.Dispatcher.VerifyAccess();
        _manager.CheckPermission(view);
        _manager.Assemble(view);
        if (viewName is not null && !_namedViews.TryAdd(viewName, view))
            throw new InvalidOperationException($"区域中已存在名为 '{viewName}' 的视图。");
        AddCore(view);
        if (ActiveView is null) Activate(view);
    }

    internal void AddCore(UIElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!Contains(view)) _views.Add(view);
    }

    internal void Remember(UIElement view, Uri uri, bool keepAlive) => _metadata[view] = (uri, keepAlive);
    internal Uri GetUri(UIElement view) => _metadata.TryGetValue(view, out var item)
        ? item.Uri : new Uri("view:///" + Guid.NewGuid().ToString("N"), UriKind.Absolute);
    internal bool IsRoute(UIElement view, string route) => _metadata.TryGetValue(view, out var item) &&
        RegionRoute.GetPath(item.Uri).Equals(route, StringComparison.OrdinalIgnoreCase);

    public void Activate(UIElement view)
    {
        Host.Dispatcher.VerifyAccess();
        if (!Contains(view)) throw new ArgumentException("视图尚未加入当前区域。", nameof(view));
        _manager.CheckPermission(view);
        Service.CancelPending();
        var old = ActiveView;
        SwitchTo(view);
        if (old is not null && !ReferenceEquals(old, view)) ReleaseIfNeeded(old);
    }

    public void Deactivate(UIElement view)
    {
        Host.Dispatcher.VerifyAccess();
        if (!ReferenceEquals(ActiveView, view)) return;
        Service.CancelPending();
        SwitchTo(null);
        ReleaseIfNeeded(view);
    }

    internal void SwitchTo(UIElement? view)
    {
        if (ReferenceEquals(ActiveView, view)) return;
        var old = ActiveView;
        var previousContent = Host.Content;
        _animationContext?.Cancel();
        if (old is IRegionView oldRegion) oldRegion.OnDeactivated();
        var currentContent = view is null ? null : _manager.GetPresentation(view);
        Host.Content = currentContent;
        _activeViews.Clear();
        if (view is not null)
        {
            AddCore(view);
            _activeViews.Add(view);
            if (view is IRegionView active) active.OnActivated();
        }

        var animationContext = new RegionAnimationContext(
            Host, old, view, previousContent, currentContent);
        _animationContext = animationContext;
        try
        {
            Animation.Animate(animationContext);
        }
        catch
        {
            animationContext.Cancel();
            if (ReferenceEquals(_animationContext, animationContext)) _animationContext = null;
            throw;
        }
    }

    private RegionAnimationContext? _animationContext;

    internal void CancelAnimation()
    {
        _animationContext?.Cancel();
        _animationContext = null;
    }

    internal void ReleaseIfNeeded(UIElement view)
    {
        if (_metadata.TryGetValue(view, out var metadata) && !metadata.KeepAlive || !KeepMemberAlive(view))
            RemoveCore(view);
    }

    private static bool KeepMemberAlive(UIElement view)
    {
        if (view is IRegionMemberLifetime member) return member.KeepAlive;
        var dataContext = (view as FrameworkElement)?.DataContext;
        if (dataContext is IRegionMemberLifetime model) return model.KeepAlive;
        return view.GetType().GetCustomAttribute<RegionMemberLifetimeAttribute>()?.KeepAlive
            ?? dataContext?.GetType().GetCustomAttribute<RegionMemberLifetimeAttribute>()?.KeepAlive
            ?? true;
    }

    internal void RemoveCore(UIElement view)
    {
        _views.Remove(view);
        _metadata.Remove(view);
        foreach (var key in _namedViews.Where(x => ReferenceEquals(x.Value, view)).Select(x => x.Key).ToArray())
            _namedViews.Remove(key);
    }

    public bool Remove(UIElement view)
    {
        Host.Dispatcher.VerifyAccess();
        if (!Contains(view)) return false;
        _manager.RemoveView(Name, view);
        return true;
    }

    public void RemoveAll()
    {
        foreach (var view in Views.ToArray()) Remove(view);
    }

    public void RequestNavigate(Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
        => NavigationService.RequestNavigate(target, callback, navigationParameters);

    public void RequestNavigate(Uri target, NavigationParameters parameters, Action<NavigationResult>? callback = null)
        => RequestNavigate(target, callback, parameters);
}
