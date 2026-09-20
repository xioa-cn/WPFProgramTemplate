using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MachineApplication.Entrance.Views;

/// <summary>标题栏命令；忽略设计器代理，仅操作运行时 WPF 窗口。</summary>
public static class WindowCommands
{
    public static ICommand ToggleTopmost { get; } =
        Create(window => window.Topmost = !window.Topmost);

    public static ICommand Minimize { get; } = Create(SystemCommands.MinimizeWindow);

    public static ICommand ToggleMaximize { get; } = Create(window =>
    {
        if (window.WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(window);
        else
            SystemCommands.MaximizeWindow(window);
    });

    public static ICommand Close { get; } = Create(SystemCommands.CloseWindow);

    private static ICommand Create(Action<Window> action) => new RelayCommand<object>(
        parameter =>
        {
            if (parameter is Window window && !DesignerProperties.GetIsInDesignMode(window))
                action(window);
        },
        parameter => parameter is Window window && !DesignerProperties.GetIsInDesignMode(window));
}

