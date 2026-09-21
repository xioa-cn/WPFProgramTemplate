using Machine.ModuleLoad;
using Microsoft.Extensions.DependencyInjection;
using Machine.ModuleLoad.Mapper;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[ModuleDataContextAttribute<MainWindowViewModel>("Common")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoginRequired;
    }

    /// <summary>首次显示主窗体必须登录，取消登录则关闭程序。</summary>
    private void OnLoginRequired(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoginRequired;
        var permissions = MainProvider.ServiceProvider?.GetService<PermissionService>();
        if (permissions is null || permissions.CurrentUser is not null) return;
        if (new LoginWindow(permissions) { Owner = this }.ShowDialog() != true) Close();
    }

    /// <summary>切换账号前清空身份和区域历史，取消后保持注销状态。</summary>
    private void SwitchAccountClick(object sender, RoutedEventArgs args)
    {
        var permissions = MainProvider.ServiceProvider?.GetRequiredService<PermissionService>();
        if (permissions is null) return;
        permissions.Logout();
        if (new LoginWindow(permissions) { Owner = this }.ShowDialog() != true) Close();
    }

    private HwndSource? _windowSource;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowMessage);
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessage);
        _windowSource = null;
        base.OnClosed(e);
    }

    private static IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (message != wmGetMinMaxInfo) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        // Both Win32 rectangles and MINMAXINFO use device pixels; do not mix in WPF DIPs.
        var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition.X = info.Work.Left - info.Monitor.Left;
        limits.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
        limits.MaxSize.X = info.Work.Right - info.Work.Left;
        limits.MaxSize.Y = info.Work.Bottom - info.Work.Top;
        Marshal.StructureToPtr(limits, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}