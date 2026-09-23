using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;


namespace Machine.ModuleLoad.Region;

/// <summary>描述一次区域切换涉及的视图和宿主内容。</summary>
/// <remarks>
/// 区域会先设置 <see cref="ContentControl.Content"/>，再调用动画策略，确保导航状态和视觉树保持同步。自定义动画可以使用
/// <see cref="PreviousContent"/> 创建覆盖层，或操作宿主模板中的其他元素。
/// </remarks>
public sealed class RegionAnimationContext
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _cleanupLock = new();
    private List<Action>? _cleanup;
    private int _cancelled;

    internal RegionAnimationContext(ContentControl host, UIElement? previousView,
        UIElement? currentView, object? previousContent, object? currentContent)
    {
        Host = host;
        PreviousView = previousView;
        CurrentView = currentView;
        PreviousContent = previousContent;
        CurrentContent = currentContent;
    }

    /// <summary>获取本次切换的内容宿主。</summary>
    public ContentControl Host { get; }

    /// <summary>获取切换前处于活动状态的视图。</summary>
    public UIElement? PreviousView { get; }

    /// <summary>获取切换后处于活动状态的视图。</summary>
    public UIElement? CurrentView { get; }

    /// <summary>获取切换前宿主中的内容。</summary>
    public object? PreviousContent { get; }

    /// <summary>获取本次切换分配给宿主的内容。</summary>
    public object? CurrentContent { get; }

    /// <summary>获取在其他切换替代本次切换时取消的令牌。</summary>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>注册自定义动画拥有的资源清理操作。</summary>
    public IDisposable RegisterCleanup(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        lock (_cleanupLock)
        {
            if (Volatile.Read(ref _cancelled) != 0)
            {
                cleanup();
                return DisposableAction.Empty;
            }

            (_cleanup ??= []).Add(cleanup);
            return new DisposableAction(() =>
            {
                lock (_cleanupLock) _cleanup?.Remove(cleanup);
            });
        }
    }

    internal void Cancel()
    {
        if (Interlocked.Exchange(ref _cancelled, 1) != 0) return;
        try
        {
            _cancellation.Cancel();
        }
        catch
        {
            // 用户注册的取消回调不能阻止区域继续切换。
        }

        Action[] cleanup;
        lock (_cleanupLock)
        {
            cleanup = _cleanup?.ToArray() ?? [];
            _cleanup?.Clear();
        }

        foreach (var action in cleanup)
        {
            try
            {
                action();
            }
            catch
            {
                /* 动画清理不得破坏后续的导航。 */
            }
        }
    }

    private sealed class DisposableAction(Action action) : IDisposable
    {
        public static readonly IDisposable Empty = new DisposableAction(static () => { });
        private Action? _action = action;
        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}

/// <summary>为区域内容切换提供动画策略。</summary>
public interface IRegionAnimation
{
    /// <summary>播放一次切换动画。方法应快速返回，动画本身可以异步运行。</summary>
    void Animate(RegionAnimationContext context);
}

/// <summary>区域默认动画，同时可作为自定义策略的基类。</summary>
/// <remarks>
/// 默认构造函数创建短时淡入动画。可以继承此类重写 <see cref="Animate"/>，
/// 或通过构造函数传入委托，并通过 <see cref="IRegion.Animation"/>、区域注册参数或 XAML 附加属性替换策略。
/// </remarks>
public class RegionAnimation : IRegionAnimation
{
    private readonly Action<RegionAnimationContext>? _transition;

    /// <summary>创建默认淡入策略。</summary>
    public RegionAnimation()
    {
    }

    /// <summary>创建由用户委托驱动的策略。</summary>
    public RegionAnimation(Action<RegionAnimationContext> transition)
        => _transition = transition ?? throw new ArgumentNullException(nameof(transition));

    /// <summary>默认淡入动画的持续时间；设置为零可禁用默认动画。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(180);

    /// <summary>默认淡入动画使用的缓动函数。</summary>
    public IEasingFunction? EasingFunction { get; set; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>禁用默认动画，同时保留策略对象。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>播放切换动画；派生类可以重写此方法。</summary>
    public virtual void Animate(RegionAnimationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_transition is not null)
        {
            _transition(context);
            return;
        }

        if (!IsEnabled || Duration <= TimeSpan.Zero || context.CurrentContent is null)
        {
            ClearOpacityAnimation(context.Host);
            return;
        }

        var host = context.Host;
        var targetOpacity = host.Opacity;
        var completed = 0;

        void Cleanup()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0) return;
            host.BeginAnimation(UIElement.OpacityProperty, null);
        }

        // 将清理动作注册到上下文，使下一次切换可以取消当前动画并恢复不透明度。
        context.RegisterCleanup(Cleanup);
        var animation = new DoubleAnimation
        {
            From = 0,
            To = targetOpacity,
            Duration = new Duration(Duration),
            EasingFunction = EasingFunction,
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => Cleanup();
        host.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>获取只应用内容、不播放动画的策略。</summary>
    public static IRegionAnimation None { get; } = new RegionAnimation(static context => ClearOpacityAnimation(context.Host));

    private static void ClearOpacityAnimation(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
    }

    public static readonly DependencyProperty AnimationProperty = DependencyProperty.RegisterAttached(
        "Animation", typeof(IRegionAnimation), typeof(RegionAnimation),
        new PropertyMetadata(null, OnAnimationChanged));

    /// <summary>获取区域宿主附加的动画策略。</summary>
    public static IRegionAnimation? GetAnimation(DependencyObject element)
        => (IRegionAnimation?)element.GetValue(AnimationProperty);

    /// <summary>设置区域宿主附加的动画策略。</summary>
    public static void SetAnimation(DependencyObject element, IRegionAnimation? value)
        => element.SetValue(AnimationProperty, value);

    private static void OnAnimationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ContentControl host ||
            RegionManager.GetRegionManager(host) is not { } manager) return;

        var region = manager.Regions.FirstOrDefault(item => ReferenceEquals(item.Host, host));
        if (region is not null)
            region.Animation = args.NewValue as IRegionAnimation ?? new RegionAnimation();
    }
}
