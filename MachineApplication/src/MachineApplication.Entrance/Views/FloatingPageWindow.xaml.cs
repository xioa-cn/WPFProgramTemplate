using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;

namespace MachineApplication.Entrance.Views;

/// <summary>可复用的无边框页面窗口，内容宿主与路由移交逻辑分离。</summary>
public partial class FloatingPageWindow : Window
{
    public FloatingPageWindow()
    {
        InitializeComponent();
        StateChanged += FloatingPageWindowStateChanged;
        Loaded += FloatingPageWindowLoaded;
        UpdateWindowCornerPreference();
    }

    public ContentControl PageHost => FloatingContent;

    // 关闭窗口统一走 RegionManager 的页面回填流程。
    private void DockClick(object sender, RoutedEventArgs args) => Close();

    private void FloatingPageWindowLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= FloatingPageWindowLoaded;
        StartWindowFrameAnimation();
    }

    private void FloatingPageWindowStateChanged(object? sender, EventArgs args) => UpdateWindowCornerPreference();

    private void UpdateWindowCornerPreference()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        FloatingWindowFrame.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(10);
        FloatingWindowFrame.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(5);
        if (WindowChrome.GetWindowChrome(this) is { } chrome)
            chrome.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(10);

        UpdateWindowContentClip();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var cornerPreference = isMaximized ? 1 : 2;
        _ = DwmSetWindowAttribute(handle, 33, ref cornerPreference, sizeof(int));
    }

    private void FloatingWindowRootSizeChanged(object sender, SizeChangedEventArgs args) => UpdateWindowContentClip();

    private void UpdateWindowContentClip()
    {
        var width = FloatingWindowRoot.ActualWidth;
        var height = FloatingWindowRoot.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var clipRadius = Math.Max(0, FloatingWindowFrame.CornerRadius.TopLeft - FloatingWindowFrame.BorderThickness.Left);
        FloatingWindowRoot.Clip = new RectangleGeometry(new Rect(0, 0, width, height), clipRadius, clipRadius);
    }

    private void StartWindowFrameAnimation()
    {
        if (FloatingWindowFrame.BorderBrush is not LinearGradientBrush gradient) return;

        var rotation = new RotateTransform(0, 0.5, 0.5);
        gradient.RelativeTransform = rotation;
        rotation.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(8)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
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
        UpdateWindowCornerPreference();
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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
