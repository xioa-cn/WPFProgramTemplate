using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CsxPad.Wpf.Helpers;

internal sealed class WindowWorkAreaBehavior : IDisposable
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    private readonly Window _window;
    private HwndSource? _source;
    private bool _disposed;

    private WindowWorkAreaBehavior(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnClosed;

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            AttachHook();
        }
    }

    public static WindowWorkAreaBehavior Attach(Window window) => new(window);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Closed -= OnClosed;
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => AttachHook();

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    private void AttachHook()
    {
        if (_source is not null)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessageHook);
    }

    private static IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        handled = TryApplyCurrentMonitorWorkArea(hwnd, lParam);
        return IntPtr.Zero;
    }

    private static bool TryApplyCurrentMonitorWorkArea(IntPtr hwnd, IntPtr minMaxInfoAddress)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(minMaxInfoAddress);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = workArea.Left - monitorArea.Left;
        minMaxInfo.MaxPosition.Y = workArea.Top - monitorArea.Top;
        minMaxInfo.MaxSize.X = workArea.Right - workArea.Left;
        minMaxInfo.MaxSize.Y = workArea.Bottom - workArea.Top;

        Marshal.StructureToPtr(minMaxInfo, minMaxInfoAddress, false);
        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect MonitorArea;
        public Rect WorkArea;
        public uint Flags;
    }
}
