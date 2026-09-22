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

    }

    /// <summary>通过框架隐藏主页、注销并显示居中的登录窗口。</summary>
    private void SwitchAccountClick(object sender, RoutedEventArgs args)
    {
        try
        {
            MainProvider.ServiceProvider!.GetRequiredService<LoginWindowFlow>().SwitchAccount();
        }
        catch (Exception ex)
        {
            // 流程服务已处理退出，记录异常但不继续向 WPF 消息循环抛出。
            Machine.ModuleLoad.Logger.GlobalLogger.Error(ex.ToString());
        }
    }
    private HwndSource? _windowSource;
    private bool _isClosed;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // SourceInitialized 事件处理器可能进入嵌套消息循环并关闭窗口。
        // 不为已销毁的窗口重新创建句柄，也不将零句柄传给 FromHwnd。
        if (_isClosed || Dispatcher.HasShutdownStarted) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var source = HwndSource.FromHwnd(handle);
        if (source is null || source.IsDisposed) return;
        _windowSource = source;
        _windowSource.AddHook(WindowMessage);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        if (_windowSource is { IsDisposed: false }) _windowSource.RemoveHook(WindowMessage);
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