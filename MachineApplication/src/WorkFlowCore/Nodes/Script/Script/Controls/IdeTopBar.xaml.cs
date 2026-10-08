using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace CsxPad.Wpf.Controls;

public partial class IdeTopBar : UserControl
{
    private System.Windows.Window? _window;

    public IdeTopBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = System.Windows.Window.GetWindow(this);
        if (_window is null) return;
        _window.StateChanged += Window_StateChanged;
        UpdateMaximizeIcon();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null) _window.StateChanged -= Window_StateChanged;
        _window = null;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (_window is not null) SystemCommands.MinimizeWindow(_window);
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_window is null) return;
        if (_window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(_window);
        else SystemCommands.MaximizeWindow(_window);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_window is not null) SystemCommands.CloseWindow(_window);
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateMaximizeIcon();

    private void UpdateMaximizeIcon()
    {
        if (_window is null) return;
        var isMaximized = _window.WindowState == WindowState.Maximized;
        MaximizeRestoreIcon.Kind = isMaximized ? PackIconKind.WindowRestore : PackIconKind.WindowMaximize;
        MaximizeRestoreButton.ToolTip = isMaximized ? "Restore" : "Maximize";
    }
}
