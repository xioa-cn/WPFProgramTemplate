using System.Windows.Controls;

namespace MachineApplication.Entrance.Components;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MachineApplication.Entrance.Models;

/// <summary>左侧导航组件，继承窗口 VM，通过命令展开菜单和导航。</summary>
public partial class LeftNavMode : UserControl
{
    private Button? _visibleSelection;
    /// <summary>初始化视图，运行时 DataContext 由父窗口提供。</summary>
    public LeftNavMode()
    {
        InitializeComponent();
        // 这里只同步视觉坐标，导航与选中状态仍由 VM 的 Command 管理。
        LayoutUpdated += UpdateSelectionArrow;
        MouseMove += (_, _) => UpdateHoverBackground();
        MouseLeave += (_, _) => UpdateHoverBackground();
        GotKeyboardFocus += (_, _) => UpdateHoverBackground();
        LostKeyboardFocus += (_, _) => UpdateHoverBackground();
        Unloaded += (_, _) => HideSelection();
    }

    /// <summary>背景与箭头作为完整选中条越过侧栏边界，折叠或滚出视口时一起隐藏。</summary>
    private void UpdateSelectionArrow(object? sender, EventArgs args)
    {
        UpdateHoverBackground();
        var selected = FindSelectedButton(MenuViewport);
        if (selected is null || selected.ActualHeight <= 0)
        {
            HideSelection();
            return;
        }

        var origin = selected.TranslatePoint(new Point(), this);
        var center = origin.Y + selected.ActualHeight / 2;
        var viewportTop = MenuViewport.TranslatePoint(new Point(), this).Y;
        const double radius = 18;
        // 整个选中条都在视口内才显示，避免覆盖顶部标题或底部语言按钮。
        var visible = origin.Y >= viewportTop && origin.Y + selected.ActualHeight <= viewportTop + MenuViewport.ActualHeight;
        if (!visible)
        {
            HideSelection();
            return;
        }
        var shouldAnimate = !ReferenceEquals(_visibleSelection, selected);
        _visibleSelection = selected;
        SelectionArrow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SelectionBackground.Visibility = SelectionArrow.Visibility;
        if (visible)
        {
            // 背景比箭头右边缘再多留 4 像素，将圆形按钮完整包在圆角末端中。
            // 背景始终从侧栏左边缘铺满，菜单层级缩进只影响文字和图标。
            Canvas.SetLeft(SelectionBackground, 0);
            Canvas.SetTop(SelectionBackground, origin.Y);
            SelectionBackground.Width = ActualWidth + 20;
            SelectionBackground.Height = selected.ActualHeight;
        }
        if (visible && Math.Abs(SelectionArrowPosition.Y - (center - radius)) > 0.1)
            SelectionArrowPosition.Y = center - radius;
        if (shouldAnimate) PlaySelectionEntrance();
    }

    /// <summary>悬停和键盘焦点背景从侧栏左端铺开，独立于菜单层级缩进。</summary>
    private void UpdateHoverBackground()
    {
        var button = FindMenuButton(MenuViewport, item => item.IsMouseOver)
            ?? FindMenuButton(MenuViewport, item => item.IsKeyboardFocused);
        if (button is null || button.DataContext is NavModel { IsSelected: true })
        {
            HoverBackground.Visibility = Visibility.Collapsed;
            return;
        }
        var origin = button.TranslatePoint(new Point(), this);
        var top = MenuViewport.TranslatePoint(new Point(), this).Y;
        // 只绘制视口内的部分，避免滚动时高亮覆盖标题和底部按钮。
        var visibleTop = Math.Max(origin.Y, top);
        var visibleBottom = Math.Min(origin.Y + button.ActualHeight, top + MenuViewport.ActualHeight);
        if (visibleBottom <= visibleTop)
        {
            HoverBackground.Visibility = Visibility.Collapsed;
            return;
        }
        Canvas.SetLeft(HoverBackground, 0);
        Canvas.SetTop(HoverBackground, origin.Y);
        HoverBackground.Width = Math.Max(0, ActualWidth - 8);
        HoverBackground.Height = button.ActualHeight;
        HoverBackground.Clip = new RectangleGeometry(new Rect(0, visibleTop - origin.Y,
            HoverBackground.Width, visibleBottom - visibleTop));
        HoverBackground.Visibility = Visibility.Visible;
    }

    /// <summary>背景和箭头使用相同的横向动画，从侧栏左侧滑入后减速停靠。</summary>
    private void PlaySelectionEntrance()
    {
        // 仅改变渲染位置，不改变布局；LayoutUpdated 不会重复启动同一选中项。
        var slide = new DoubleAnimation
        {
            From = -ActualWidth - 24,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(580),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        SelectionBackgroundPosition.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
        SelectionArrowPosition.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>折叠、滚出视口或卸载时取消动画，下一次出现时重新滑入。</summary>
    private void HideSelection()
    {
        _visibleSelection = null;
        SelectionArrow.Visibility = Visibility.Collapsed;
        SelectionBackground.Visibility = Visibility.Collapsed;
        SelectionBackgroundPosition.BeginAnimation(TranslateTransform.XProperty, null);
        SelectionArrowPosition.BeginAnimation(TranslateTransform.XProperty, null);
    }

    /// <summary>查找展开层级中的选中菜单按钮，跳过已折叠的分支。</summary>
    private static Button? FindSelectedButton(DependencyObject parent)
        => FindMenuButton(parent, button => button.DataContext is NavModel { IsSelected: true });

    /// <summary>按视觉状态查找菜单按钮，折叠分支不参与命中。</summary>
    private static Button? FindMenuButton(DependencyObject parent, Func<Button, bool> matches)
    {
        if (parent is UIElement element && element.Visibility != Visibility.Visible) return null;
        if (parent is Button { DataContext: NavModel } button && matches(button)) return button;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var result = FindMenuButton(VisualTreeHelper.GetChild(parent, index), matches);
            if (result is not null) return result;
        }
        return null;
    }
}
