using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Machine.ModuleLoad.Region;

/// <summary>完成确认、内容解析、激活、历史提交和生命周期通知。</summary>
public sealed class RegionNavigationService : IRegionNavigationService
{
    private readonly RegionManager _manager;
    private RegionInfo _region;
    private Request? _pending;
    internal RegionNavigationContext? CurrentContext { get; private set; }
    internal RegionNavigationJournal History { get; }
    /// <summary>
    /// 获取或设置服务当前绑定的区域。
    /// </summary>
    /// <remarks>
    /// Prism 允许区域行为在初始化时注入区域。这里的区域必须由同一个
    /// <see cref="RegionManager"/> 创建，避免导航服务与权限、视图发现使用不同的管理器。
    /// </remarks>
    public IRegion Region
    {
        get => _region;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value is not RegionInfo region || !ReferenceEquals(region.RegionManager, _manager))
                throw new ArgumentException("区域必须是当前 RegionManager 创建的区域。", nameof(value));
            _region = region;
        }
    }
    public IRegionNavigationJournal Journal => History;
    public Uri? CurrentSource => CurrentContext?.Uri;
    public Uri? CurrentUri => CurrentSource;
    public event EventHandler<RegionNavigationEventArgs>? Navigating;
    public event EventHandler<RegionNavigationEventArgs>? Navigated;
    public event EventHandler<RegionNavigationFailedEventArgs>? NavigationFailed;

    internal RegionNavigationService(RegionManager manager, RegionInfo region)
    {
        _manager = manager;
        _region = region;
        Region = region;
        History = new(this);
        region.Host.Unloaded += OnHostUnloaded;
    }

    public void RequestNavigate(Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        Dispatch(() =>
        {
            var context = new RegionNavigationContext(this, target, navigationParameters);
            Start(context, () => _manager.ResolveContent(_region, context), true, false, callback, null);
        });
    }

    public void RequestNavigate(Uri target, NavigationParameters parameters, Action<NavigationResult>? callback = null)
        => RequestNavigate(target, callback, parameters);

    internal UIElement NavigateResolved(UIElement view, Uri target, NavigationParameters? parameters,
        bool keepAlive, bool direct, Action<NavigationResult>? callback = null)
    {
        _region.Host.Dispatcher.VerifyAccess();
        var context = new RegionNavigationContext(this, target, parameters);
        Exception? failure = null;
        Start(context, () => view, keepAlive, direct, result =>
        {
            failure = result.Error;
            callback?.Invoke(result);
        }, null, false);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return view;
    }

    internal void Replay(RegionNavigationJournalEntry entry, Action commit, Action<NavigationResult> callback)
    {
        Dispatch(() =>
        {
            var context = new RegionNavigationContext(this, entry.Uri, entry.Parameters);
            UIElement Resolve()
            {
                if (entry.DirectViewType is null) return _manager.ResolveContent(_region, context);
                if (entry.DirectView?.TryGetTarget(out var view) == true && _region.Contains(view) &&
                    RegionManager.NavigationAware(view).All(item => item.IsNavigationTarget(context)))
                    return view;
                return _manager.CreateView(entry.DirectViewType);
            }
            Start(context, Resolve, entry.KeepAlive, entry.DirectViewType is not null, callback, commit);
        });
    }

    private void Start(RegionNavigationContext context, Func<UIElement> resolve, bool keepAlive, bool direct,
        Action<NavigationResult>? callback, Action? journalCommit, bool useLoadingAnimation = true)
    {
        var request = new Request(context, callback);
        var previous = _pending;
        _pending = request;
        if (previous is not null) Complete(previous, false);
        if (!IsCurrent(request)) return;
        var outgoing = _region.ActiveViews.ToArray();
        var confirmations = outgoing.SelectMany(RegionManager.NavigationAware).OfType<IConfirmNavigationRequest>().ToArray();

        void Confirm(int index)
        {
            if (!IsCurrent(request)) return;
            if (index == confirmations.Length)
            {
                Execute();
                return;
            }
            var called = 0;
            confirmations[index].ConfirmNavigationRequest(context, allowed =>
            {
                if (Interlocked.Exchange(ref called, 1) != 0) return;
                Dispatch(() => Guard(request, () =>
                {
                    if (allowed) Confirm(index + 1);
                    else Complete(request, false);
                }));
            });
        }

        async void Execute()
        {
            try
            {
                if (useLoadingAnimation && _region.Animation is IRegionLoadingAnimation animation)
                {
                    request.LoadingContext = _region.BeginLoading();
                    animation.BeginLoading(request.LoadingContext);
                    await WaitForPresentationAsync(request);
                }

                await LoadContentAsync();
                if (!IsCurrent(request)) return;
                if (request.LoadingContext is not null)
                    await WaitForPresentationAsync(request);
            }
            catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                if (IsCurrent(request)) Complete(request, false, error);
                return;
            }

            if (IsCurrent(request)) Complete(request, true);
        }

        async Task LoadContentAsync()
        {
            if (!IsCurrent(request)) return;
            var view = resolve();
            if (!IsCurrent(request)) return;
            _manager.Assemble(view);
            _manager.CheckPermission(view);
            // 激活浮动页面不替换区域当前内容，也不写入该区域的历史。
            if (_manager.FocusFloating(view))
            {
                return;
            }

            var participants = new object?[] { view, (view as FrameworkElement)?.DataContext };
            foreach (var participant in participants.OfType<IAsyncNavigationAware>()
                         .Distinct<IAsyncNavigationAware>(ReferenceEqualityComparer.Instance))
            {
                await participant.PrepareAsync(context, request.CancellationToken);
                if (!IsCurrent(request)) return;
            }

            Navigating?.Invoke(this, new(context));
            if (!IsCurrent(request)) return;
            foreach (var item in outgoing.SelectMany(RegionManager.NavigationAware))
            {
                item.OnNavigatedFrom(context);
                if (!IsCurrent(request)) return;
            }

            _region.Remember(view, context.Uri, keepAlive);
            _region.SwitchTo(view, request.LoadingContext);
            if (!IsCurrent(request)) return;
            foreach (var old in outgoing)
                if (!ReferenceEquals(old, view)) _region.ReleaseIfNeeded(old);

            CurrentContext = context;
            if (journalCommit is not null) journalCommit();
            else History.RecordNavigation(new RegionNavigationJournalEntry(context.Uri, context.Parameters)
            {
                DirectView = direct ? new WeakReference<UIElement>(view) : null,
                DirectViewType = direct ? view.GetType() : null,
                KeepAlive = keepAlive
            });
            foreach (var item in RegionManager.NavigationAware(view))
            {
                item.OnNavigatedTo(context);
                if (!IsCurrent(request)) return;
            }
        }

        Guard(request, () => Confirm(0));
    }

    private bool IsCurrent(Request request) => !request.Completed && ReferenceEquals(_pending, request);

    private async Task WaitForPresentationAsync(Request request)
    {
        var host = request.LoadingContext!.Host;
        await host.Dispatcher.InvokeAsync(() =>
        {
            host.ApplyTemplate();
            if (!host.IsMeasureValid || !host.IsArrangeValid)
                host.UpdateLayout();
        }, DispatcherPriority.Loaded, request.CancellationToken);

        await host.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded,
            request.CancellationToken);
    }

    private void OnHostUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement host || _pending is not { } request) return;
        host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!host.IsLoaded && IsCurrent(request)) Complete(request, false);
        }));
    }

    private void Guard(Request request, Action action)
    {
        if (!IsCurrent(request)) return;
        try { action(); }
        catch (Exception error)
        {
            // 完成回调或事件处理器的异常不能再次触发完成回调。
            if (request.Completed) throw;
            Complete(request, false, error);
        }
    }

    private void Complete(Request request, bool success, Exception? error = null)
    {
        if (request.Completed) return;
        request.Completed = true;
        if (ReferenceEquals(_pending, request)) _pending = null;
        request.LoadingContext?.Cancel();
        request.Cancel();
        try
        {
            if (success) Navigated?.Invoke(this, new(request.Context));
            else NavigationFailed?.Invoke(this, new(request.Context, error));
        }
        finally { request.Callback?.Invoke(new(request.Context, success, error)); }
    }

    internal void CancelPending()
    {
        if (_pending is { } request) Complete(request, false);
    }

    internal void ClearCurrent() => CurrentContext = null;

    private void Dispatch(Action action)
    {
        if (_region.Host.Dispatcher.CheckAccess()) action();
        else _region.Host.Dispatcher.BeginInvoke(action);
    }

    private sealed class Request
    {
        private readonly CancellationTokenSource _cancellation = new();

        public Request(RegionNavigationContext context, Action<NavigationResult>? callback)
        {
            Context = context;
            Callback = callback;
            CancellationToken = _cancellation.Token;
        }

        public RegionNavigationContext Context { get; }
        public Action<NavigationResult>? Callback { get; }
        public CancellationToken CancellationToken { get; }
        public RegionAnimationContext? LoadingContext { get; set; }
        public bool Completed { get; set; }

        public void Cancel()
        {
            try { _cancellation.Cancel(); }
            catch (AggregateException) { }
            finally { _cancellation.Dispose(); }
        }
    }
}
