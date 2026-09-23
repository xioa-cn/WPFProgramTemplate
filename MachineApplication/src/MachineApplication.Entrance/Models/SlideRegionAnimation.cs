using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Machine.ModuleLoad.Region;

namespace MachineApplication.Entrance.Models;

/// <summary>
/// 区域内容切换时的滑入动画。
/// </summary>
/// <remarks>
/// <para>
/// 区域切换流程会先把新内容设置到 <see cref="RegionAnimationContext.Host"/>，
/// 然后调用此策略。因此动画作用于区域宿主，可以覆盖普通视图和 Page 视图。
/// </para>
/// <para>
/// 该类支持通过 XAML 创建，例如：
/// <c>&lt;local:SlideRegionAnimation FromX="40" Duration="0:0:0.35" /&gt;</c>。
/// </para>
/// </remarks>
public sealed class SlideRegionAnimation : IRegionAnimation
{
    /// <summary>
    /// 创建一个默认从右侧向左滑入的区域动画。
    /// </summary>
    public SlideRegionAnimation()
    {
    }

    /// <summary>
    /// 动画持续时间。小于或等于零时不播放动画。
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// 动画开始时的横向偏移量，单位为设备无关像素。
    /// 正值表示从右侧向左滑入，负值表示从左侧向右滑入。
    /// </summary>
    public double FromX { get; set; } = 40;

    /// <summary>
    /// 动画开始时的纵向偏移量，单位为设备无关像素。
    /// 正值表示从下方向上滑入，负值表示从上方向下滑入。
    /// </summary>
    public double FromY { get; set; }

    /// <summary>
    /// 是否启用滑入动画。设置为 <see langword="false"/> 时只显示新内容。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 控制滑动速度变化的缓动函数。
    /// </summary>
    public IEasingFunction? EasingFunction { get; set; } =
        new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// 播放一次区域内容滑入动画。
    /// </summary>
    /// <param name="context">当前区域切换的上下文。</param>
    public void Animate(RegionAnimationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Host;
        var previousTransform = host.RenderTransform;
        var slideTransform = new TranslateTransform(FromX, FromY);
        var completed = 0;

        // 还原区域原本的变换，避免动画结束后留下 TranslateTransform。
        void Cleanup()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            slideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            slideTransform.BeginAnimation(TranslateTransform.YProperty, null);

            // 如果动画期间没有其他代码替换 RenderTransform，才恢复原值，
            // 避免覆盖外部在动画期间设置的新变换。
            if (ReferenceEquals(host.RenderTransform, slideTransform))
                host.RenderTransform = previousTransform;
        }

        // 导航被下一次切换取消时，RegionAnimationContext 会调用此清理操作。
        context.RegisterCleanup(Cleanup);

        if (!IsEnabled || Duration <= TimeSpan.Zero || context.CurrentContent is null)
        {
            Cleanup();
            return;
        }

        host.RenderTransform = slideTransform;

        var xAnimation = new DoubleAnimation
        {
            From = FromX,
            To = 0,
            Duration = new Duration(Duration),
            EasingFunction = EasingFunction,
            FillBehavior = FillBehavior.Stop
        };

        var yAnimation = new DoubleAnimation
        {
            From = FromY,
            To = 0,
            Duration = new Duration(Duration),
            EasingFunction = EasingFunction,
            FillBehavior = FillBehavior.Stop
        };

        // X 动画完成时两条轴的动画都应当已经结束，因此统一执行清理。
        xAnimation.Completed += (_, _) => Cleanup();
        slideTransform.BeginAnimation(
            TranslateTransform.XProperty,
            xAnimation,
            HandoffBehavior.SnapshotAndReplace);
        slideTransform.BeginAnimation(
            TranslateTransform.YProperty,
            yAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }
}
