using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Machine.ModuleLoad.Region;
using RestSharp;

namespace MachineApplication.Entrance.Components;

/// <summary>使用 Naive UI LoadingBar 的视觉参数和状态转换的顶部加载条。</summary>
/// <remarks>放在最外层 Grid 中，作为覆盖层使用，不拦截输入。使用 Start、Finish、Error 控制加载状态。</remarks>
public sealed class LoadingBar : FrameworkElement, ILoadingBar
{
    private static readonly TimeSpan StartDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FinishDuration = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan EnterDuration = TimeSpan.FromSeconds(0.3);
    private static readonly TimeSpan LeaveDuration = TimeSpan.FromSeconds(0.8);
    private static readonly Color LightLoadingColor = Color.FromRgb(0x18, 0xa0, 0x58);
    private static readonly Color LightErrorColor = Color.FromRgb(0xd0, 0x30, 0x50);
    private static readonly Color DarkLoadingColor = Color.FromRgb(0x63, 0xe2, 0xb7);

    public static readonly DependencyProperty LoadingColorProperty = DependencyProperty.Register(
        nameof(LoadingColor), typeof(Color?), typeof(LoadingBar),
        new FrameworkPropertyMetadata(null, OnColorsChanged));

    public static readonly DependencyProperty ErrorColorProperty = DependencyProperty.Register(
        nameof(ErrorColor), typeof(Color?), typeof(LoadingBar),
        new FrameworkPropertyMetadata(null, OnColorsChanged));

    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.Register(
        nameof(IsDark), typeof(bool), typeof(LoadingBar),
        new FrameworkPropertyMetadata(false, OnColorsChanged));

    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(LoadingBar),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsMeasure |
                                          FrameworkPropertyMetadataOptions.AffectsRender),
        static value => value is double height && double.IsFinite(height) && height >= 0);

    private static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register(
        "ProgressValue", typeof(double), typeof(LoadingBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty BarOpacityProperty = DependencyProperty.Register(
        "BarOpacity", typeof(double), typeof(LoadingBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty BarColorProperty = DependencyProperty.Register(
        "BarColor", typeof(Color), typeof(LoadingBar),
        new FrameworkPropertyMetadata(LightLoadingColor, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyPropertyKey IsLoadingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsLoading), typeof(bool), typeof(LoadingBar), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsLoadingProperty = IsLoadingPropertyKey.DependencyProperty;

    private readonly SolidColorBrush _brush = new();
    private long _operation;
    private Phase _phase;
    private bool _entering;
    private bool _leaving;

    static LoadingBar()
    {
        VerticalAlignmentProperty.OverrideMetadata(typeof(LoadingBar),
            new FrameworkPropertyMetadata(VerticalAlignment.Top));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(LoadingBar), new FrameworkPropertyMetadata(false));
        VisibilityProperty.OverrideMetadata(typeof(LoadingBar), new FrameworkPropertyMetadata(Visibility.Collapsed));
        Panel.ZIndexProperty.OverrideMetadata(typeof(LoadingBar), new FrameworkPropertyMetadata(5999));
    }

    public LoadingBar()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Unloaded += (_, _) => ResetCore();
    }

    /// <summary>自定义加载颜色；null 时使用 Naive UI 当前明暗主题的默认颜色。</summary>
    public Color? LoadingColor
    {
        get => (Color?)GetValue(LoadingColorProperty);
        set => SetValue(LoadingColorProperty, value);
    }

    /// <summary>自定义错误颜色；null 时浅色主题为 #d03050，深色主题为 red。</summary>
    public Color? ErrorColor
    {
        get => (Color?)GetValue(ErrorColorProperty);
        set => SetValue(ErrorColorProperty, value);
    }

    public bool IsDark
    {
        get => (bool)GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    /// <summary>对应 Naive UI 的 height 主题变量，默认 2 个设备无关像素。</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public bool IsLoading => (bool)GetValue(IsLoadingProperty);

    public double Progress => (double)GetValue(ProgressValueProperty);

    private Color CurrentColor => (Color)GetValue(BarColorProperty);

    private Color TargetColor => _phase == Phase.Error
        ? ErrorColor ?? (IsDark ? Colors.Red : LightErrorColor)
        : LoadingColor ?? (IsDark ? DarkLoadingColor : LightLoadingColor);

    /// <summary>从 0 开始，以 4 秒线性推进到 80%，保持到完成或失败。</summary>
    public void Start() => OnDispatcher(() => BeginCycle(false));

    /// <summary>以 0.2 秒推进到 100%，同时以 0.8 秒淡出；入场未结束时先等待入场。</summary>
    public void Finish() => OnDispatcher(() =>
    {
        if (_phase != Phase.Starting) return;
        _phase = Phase.Finishing;
        SetValue(IsLoadingPropertyKey, false);
        AnimateProgress(100, FinishDuration);
        if (!_entering) FadeOut();
    });

    /// <summary>切换错误色并完成进度后隐藏；未开始时也能显示完整错误条。</summary>
    public void Error() => OnDispatcher(() =>
    {
        if (_phase is Phase.Finishing or Phase.Error) return;
        if (_phase == Phase.Idle)
        {
            BeginCycle(true);
            return;
        }

        _phase = Phase.Error;
        SetValue(IsLoadingPropertyKey, false);
        AnimateColor(TargetColor);
        AnimateProgress(100, FinishDuration);
        if (!_entering) FadeOut();
    });

    /// <summary>立即停止并隐藏，不播放离场动画。</summary>
    public void Reset() => OnDispatcher(ResetCore);

    protected override Size MeasureOverride(Size availableSize) => new(0, BarHeight);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_phase == Phase.Idle || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;

        _brush.Color = CurrentColor;
        drawingContext.PushOpacity((double)GetValue(BarOpacityProperty));
        drawingContext.DrawRectangle(_brush, null,
            new Rect(0, 0, RenderSize.Width * Math.Clamp(Progress, 0, 100) / 100,
                Math.Min(RenderSize.Height, BarHeight)));
        drawingContext.Pop();
    }

    private void BeginCycle(bool error)
    {
        ++_operation;
        StopAnimations();
        _phase = error ? Phase.Error : Phase.Starting;
        _entering = true;
        _leaving = false;
        SetValue(IsLoadingPropertyKey, !error);
        SetValue(ProgressValueProperty, error ? 100d : 0d);
        SetValue(BarOpacityProperty, 0d);
        SetValue(BarColorProperty, TargetColor);
        SetCurrentValue(VisibilityProperty, Visibility.Visible);

        AnimateOpacity(1, EnterDuration, () =>
        {
            _entering = false;
            if (_phase is Phase.Finishing or Phase.Error) FadeOut();
        });
        if (!error) AnimateProgress(80, StartDuration);
    }

    private void AnimateProgress(double target, TimeSpan duration)
    {
        var current = Progress;
        SetValue(ProgressValueProperty, target);
        BeginAnimation(ProgressValueProperty, new DoubleAnimation(current, target, new Duration(duration))
        {
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateColor(Color target)
    {
        var current = CurrentColor;
        SetValue(BarColorProperty, target);
        BeginAnimation(BarColorProperty, new ColorAnimation(current, target, new Duration(FinishDuration))
        {
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateOpacity(double target, TimeSpan duration, Action completed)
    {
        var operation = _operation;
        var current = (double)GetValue(BarOpacityProperty);
        SetValue(BarOpacityProperty, target);
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(duration),
            FillBehavior = FillBehavior.Stop
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(current, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(target, KeyTime.FromTimeSpan(duration),
            new KeySpline(0.4, 0, 0.2, 1)));
        animation.Completed += (_, _) =>
        {
            if (operation != _operation) return;
            BeginAnimation(BarOpacityProperty, null);
            completed();
        };
        BeginAnimation(BarOpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void FadeOut()
    {
        if (_leaving) return;
        _leaving = true;
        AnimateOpacity(0, LeaveDuration, ResetCore);
    }

    private void ResetCore()
    {
        ++_operation;
        StopAnimations();
        _phase = Phase.Idle;
        _entering = false;
        _leaving = false;
        SetValue(IsLoadingPropertyKey, false);
        SetValue(ProgressValueProperty, 0d);
        SetValue(BarOpacityProperty, 0d);
        SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
    }

    private void StopAnimations()
    {
        BeginAnimation(ProgressValueProperty, null);
        BeginAnimation(BarOpacityProperty, null);
        BeginAnimation(BarColorProperty, null);
    }

    private void OnDispatcher(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.InvokeAsync(action);
    }

    private static void OnColorsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var bar = (LoadingBar)sender;
        if (bar._phase == Phase.Idle) bar.SetValue(BarColorProperty, bar.TargetColor);
        else bar.AnimateColor(bar.TargetColor);
    }

    private enum Phase
    {
        Idle,
        Starting,
        Finishing,
        Error
    }

    /// <summary>
    /// 显示加载条并执行同步操作；成功时返回 Unit，失败时显示错误状态并返回异常。
    /// </summary>
    public Result<Unit, Exception> Loading(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Start();
        try
        {
            action();
            Finish();
            return Result<Unit, Exception>.Ok(Unit.Value);
        }
        catch (Exception exception)
        {
            Error();
            return Result<Unit, Exception>.Err(exception);
        }
    }

    /// <summary>
    /// 显示加载条并等待异步操作；成功时返回 Unit，失败时显示错误状态并返回异常。
    /// </summary>
    public async Task<Result<Unit, Exception>> LoadingAsync(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        Start();
        try
        {
            await func();
            Finish();
            return Result<Unit, Exception>.Ok(Unit.Value);
        }
        catch (Exception exception)
        {
            Error();
            return Result<Unit, Exception>.Err(exception);
        }
    }
}
