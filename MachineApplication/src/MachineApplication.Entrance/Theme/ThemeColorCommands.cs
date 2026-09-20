using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace MachineApplication.Entrance.Theme;

/// <summary>连接色块按钮与波纹动画，视图坐标处理留在视图层，颜色状态交给 ViewModel。</summary>
public static class ThemeColorCommands
{
    /// <summary>颜色调整开关在截图后执行 VM 命令，复用主题切换的波纹效果。</summary>
    public static ICommand ToggleAdjustment { get; } = new RelayCommand<ToggleButton>(button =>
    {
        if (button?.Tag is not ICommand command || !command.CanExecute(null)) return;
        ThemeRippleAnimationHelper.ToggleThemeWithRipple(button, () => command.Execute(null));
    });
    /// <summary>通过主题波纹动画执行明暗主题切换。</summary>
    public static ICommand ToggleMode { get; } = new RelayCommand<Button>(button =>
    {
        if (button?.Tag is not ICommand command || !command.CanExecute(null)) return;
        // 用户在上一段波纹尚未结束时点击，排队到动画完成，避免出现“偶尔无效”。
        void Start()
        {
            if (ThemeRippleAnimationHelper.ToggleThemeWithRipple(button, () => command.Execute(null))) return;
            var retry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            retry.Tick += (_, _) =>
            {
                if (ThemeRippleAnimationHelper.ToggleThemeWithRipple(button, () => command.Execute(null)))
                    retry.Stop();
            };
            retry.Start();
        }
        Start();
    });

    /// <summary>按钮的 DataContext 为色阶，Tag 为 ViewModel 的选色命令。</summary>
    public static ICommand Apply { get; } = new RelayCommand<Button>(button =>
    {
        if (button?.DataContext is not ThemeColorOption color || color.IsSelected ||
            button.Tag is not ICommand command || !command.CanExecute(color)) return;

        ThemeRippleAnimationHelper.ToggleThemeWithRipple(button, () => command.Execute(color));
    });
}
