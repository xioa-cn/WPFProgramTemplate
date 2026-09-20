using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MachineApplication.Entrance.Views;

/// <summary>标题栏窗口命令，仅操作传入窗口，不持有窗口或业务 ViewModel 引用。</summary>
public static class WindowCommands
{
    /// <summary>切换窗口置顶状态。</summary>
    public static ICommand ToggleTopmost { get; } = new RelayCommand<Window>(
        window => { if (window is not null) window.Topmost = !window.Topmost; });

    /// <summary>最小化窗口。</summary>
    public static ICommand Minimize { get; } = new RelayCommand<Window>(
        window => { if (window is not null) SystemCommands.MinimizeWindow(window); });

    /// <summary>在最大化和还原之间切换，保留系统窗口恢复位置。</summary>
    public static ICommand ToggleMaximize { get; } = new RelayCommand<Window>(window =>
    {
        if (window is null) return;
        if (window.WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(window);
        else
            SystemCommands.MaximizeWindow(window);
    });

    /// <summary>关闭窗口，遵循 WPF Closing 事件的取消处理。</summary>
    public static ICommand Close { get; } = new RelayCommand<Window>(
        window => { if (window is not null) SystemCommands.CloseWindow(window); });
}
