using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Machine.ModuleLoad.Logger;

namespace MachineApplication.Entrance.Theme;

/// <summary>先保留旧主题截图，再通过圆形波纹逐步显露切换后的窗口内容。</summary>
public static class ThemeRippleAnimationHelper
{
    /// <summary>窗口级动画标记，阻止同一窗口同时创建多个波纹。</summary>
    public static readonly DependencyProperty IsThemeAnimatingProperty =
        DependencyProperty.RegisterAttached("IsThemeAnimating", typeof(bool),
            typeof(ThemeRippleAnimationHelper), new PropertyMetadata(false));

    /// <summary>读取窗口是否正在播放主题动画。</summary>
    public static bool GetIsThemeAnimating(DependencyObject obj) => (bool)obj.GetValue(IsThemeAnimatingProperty);

    /// <summary>设置窗口的主题动画状态。</summary>
    public static void SetIsThemeAnimating(DependencyObject obj, bool value) => obj.SetValue(IsThemeAnimatingProperty, value);

    /// <summary>从触发控件中心播放换色波纹；未具备渲染条件时直接执行换色。</summary>
    /// <param name="triggerElement">触发换色的控件，必须在其 UI 线程调用。</param>
    /// <param name="action">截图完成后执行的主题更新操作。</param>
    /// <param name="pixelsPerSecond">波纹直径每秒扩展的距离，必须为有限正数。</param>
    /// <param name="easing">可选缓动函数，默认使用正弦减速。</param>
    /// <returns>接受换色操作时返回 true；已有动画正在播放时返回 false。</returns>
    public static bool ToggleThemeWithRipple(UIElement triggerElement, Action action,
        double pixelsPerSecond = 3500, IEasingFunction? easing = null)
    {
        ArgumentNullException.ThrowIfNull(triggerElement);
        ArgumentNullException.ThrowIfNull(action);
        triggerElement.VerifyAccess();
        if (!double.IsFinite(pixelsPerSecond) || pixelsPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerSecond));

        var window = Window.GetWindow(triggerElement);
        if (window is not null && GetIsThemeAnimating(window))
        {
            GlobalLogger.DebuggerLogger?.Debug("Skipped theme ripple: an animation is already running.");
            return false;
        }

        // 显式 AdornerDecorator 包住整个窗口内容，截图和装饰器共用同一个坐标系。
        // 不从按钮取最近的装饰层，避免波纹被嵌套的页面或滚动区域限制。
        var root = window?.Content is AdornerDecorator decorator
            ? decorator.Child as FrameworkElement
            : window?.Content as FrameworkElement;
        var layer = root is null ? null : AdornerLayer.GetAdornerLayer(root);
        if (window is null || root is null || layer is null || root.ActualWidth < 1 || root.ActualHeight < 1)
        {
            GlobalLogger.DebuggerLogger?.Debug(
                $"Skipped theme ripple: window={window is not null}, root={root is not null}, " +
                $"adornerLayer={layer is not null}, size={root?.ActualWidth}x{root?.ActualHeight}. Applying theme directly.");
            action();
            return true;
        }

        RippleEffect? ripple = null;
        RenderTargetBitmap? bitmap = null;
        var cleanedUp = false;

        // 正常完成、窗口关闭和内容移除都释放截图，并恢复可再次换色的状态。
        void Cleanup(object? sender, EventArgs args)
        {
            if (cleanedUp) return;
            cleanedUp = true;
            window.Closed -= Cleanup;
            root.Unloaded -= Cleanup;
            if (ripple is not null)
            {
                ripple.Completed -= Cleanup;
                ripple.BeginAnimation(RippleEffect.DiameterProperty, null);
                layer.Remove(ripple);
                if (ripple.OuterBrush is ImageBrush brush) brush.ImageSource = null;
            }
            bitmap?.Clear();
            SetIsThemeAnimating(window, false);
            GlobalLogger.DebuggerLogger?.Debug("Released theme ripple overlay and screenshot.");
        }

        SetIsThemeAnimating(window, true);
        try
        {
            var dpi = Dpi.GetFromVisual(root);
            bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(root.ActualWidth * dpi.FactorX),
                (int)Math.Ceiling(root.ActualHeight * dpi.FactorY), dpi.X, dpi.Y, PixelFormats.Pbgra32);
            bitmap.Render(root);
            ripple = new RippleEffect(root)
            {
                Center = triggerElement.TranslatePoint(new Point(triggerElement.RenderSize.Width / 2,
                    triggerElement.RenderSize.Height / 2), root),
                OuterBrush = new ImageBrush(bitmap),
                IsHitTestVisible = false
            };
            ripple.Completed += Cleanup;
            window.Closed += Cleanup;
            root.Unloaded += Cleanup;
            layer.Add(ripple);

            // 先覆盖旧画面，再更新主题；扩散圆内透明，逐步露出新颜色。
            action();
            root.UpdateLayout();
            ripple.Play(pixelsPerSecond, easing ?? new SineEase { EasingMode = EasingMode.EaseOut });
            GlobalLogger.DebuggerLogger?.Debug("Started theme color ripple animation.");
            return true;
        }
        catch (Exception exception)
        {
            Cleanup(null, EventArgs.Empty);
            GlobalLogger.Error("Failed to animate theme color change.", exception);
            throw;
        }
    }
}
