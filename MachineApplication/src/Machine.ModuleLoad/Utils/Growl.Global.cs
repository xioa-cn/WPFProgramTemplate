using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Machine.ModuleLoad.Utils;

public sealed partial class Growl
{
    private static DesktopHost? _desktopHost;

    public static void InfoGlobal(string message) => ShowGlobal(new GrowlInfo { Message = message });

    public static void SuccessGlobal(string message) =>
        ShowGlobal(new GrowlInfo { Message = message, Type = GrowlType.Success });

    public static void WarningGlobal(string message) =>
        ShowGlobal(new GrowlInfo { Message = message, Type = GrowlType.Warning });

    public static void ErrorGlobal(string message) =>
        ShowGlobal(new GrowlInfo { Message = message, Type = GrowlType.Error });

    public static void FatalGlobal(string message) =>
        ShowGlobal(new GrowlInfo { Message = message, Type = GrowlType.Fatal, Duration = TimeSpan.Zero });

    public static void ShowGlobal(GrowlInfo info)
    {
        Validate(info);
        OnApplicationDispatcher(() =>
        {
            _desktopHost ??= new DesktopHost();
            try { _desktopHost.Show(info); }
            catch
            {
                _desktopHost?.Dispose();
                throw;
            }
        });
    }

    public static void ClearGlobal() => OnApplicationDispatcher(() => _desktopHost?.Dispose());

    private sealed class DesktopHost : IDisposable
    {
        private const int PopupVisibleStyle = unchecked((int)0x90000000);
        private const int TopmostToolWindowNoActivateStyle = 0x08000088;
        private const uint ShowWithoutActivationOrResize = 0x0051;
        private const int MouseActivateMessage = 0x0021;
        private const int MouseNoActivate = 3;
        private readonly Growl _host;
        private readonly HwndSource _source;
        private readonly Queue<GrowlInfo> _pending = new();
        private DispatcherOperation? _layoutOperation;
        private bool _disposed;

        public DesktopHost()
        {
            var workArea = GetWorkArea();
            _host = new Growl
            {
                _isGlobal = true,
                Width = 360,
                Visibility = Visibility.Visible
            };
            var localHost = Hosts.Select(reference => reference.TryGetTarget(out var host) ? host : null)
                .OfType<Growl>().LastOrDefault(host => host.IsLoaded && host.Token is null);
            if (localHost is not null)
                _host.SetBinding(CloseTextProperty, new Binding(nameof(CloseText)) { Source = localHost });

            _source = new HwndSource(new HwndSourceParameters("MachineApplication.Growl.Global")
            {
                WindowStyle = PopupVisibleStyle,
                ExtendedWindowStyle = TopmostToolWindowNoActivateStyle,
                PositionX = workArea.Right - 1,
                PositionY = workArea.Top,
                Width = 1,
                Height = 1,
                UsesPerPixelOpacity = true
            });
            try
            {
                _source.CompositionTarget.BackgroundColor = Colors.Transparent;
                _source.SizeToContent = SizeToContent.WidthAndHeight;
                _source.AddHook(WindowProc);
                _host.Loaded += HostLoaded;
                _host.SizeChanged += HostSizeChanged;
                _host.ItemsChanged += HostItemsChanged;
                _host.Dispatcher.ShutdownStarted += DispatcherShutdown;
                _source.RootVisual = _host;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Show(GrowlInfo info)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_host.IsLoaded) _host.Push(info);
            else
            {
                while (_pending.Count >= _host.MaxCount) _pending.Dequeue();
                _pending.Enqueue(info);
            }
            QueueLayout();
        }

        private void HostLoaded(object sender, RoutedEventArgs args)
        {
            while (!_disposed && _pending.TryDequeue(out var info)) _host.Push(info);
            QueueLayout();
        }

        private void HostSizeChanged(object sender, SizeChangedEventArgs args) => QueueLayout();

        private void HostItemsChanged(object? sender, EventArgs args) => QueueLayout();

        private void DispatcherShutdown(object? sender, EventArgs args) => Dispose();

        private IntPtr WindowProc(IntPtr window, int message, IntPtr parameter, IntPtr data, ref bool handled)
        {
            if (message is 0x001A or 0x007E or 0x02E0) QueueLayout();
            if (message == MouseActivateMessage)
            {
                handled = true;
                return new IntPtr(MouseNoActivate);
            }
            return IntPtr.Zero;
        }

        private void QueueLayout()
        {
            if (_disposed || _host.Dispatcher.HasShutdownStarted ||
                _layoutOperation is { Status: DispatcherOperationStatus.Pending }) return;
            _layoutOperation = _host.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateLayout));
        }

        private void UpdateLayout()
        {
            _layoutOperation = null;
            if (_disposed) return;
            if (_host._items.Children.Count == 0)
            {
                if (_pending.Count == 0) Dispose();
                return;
            }
            var workArea = GetWorkArea();
            var transform = _source.CompositionTarget.TransformToDevice;
            var scaleX = transform.M11;
            var scaleY = transform.M22;
            var marginX = (int)Math.Ceiling(16 * scaleX);
            var marginY = (int)Math.Ceiling(16 * scaleY);
            _host.Width = Math.Max(1, Math.Min(360, (workArea.Right - workArea.Left - 2 * marginX) / scaleX));
            _host.MaxHeight = Math.Max(1, (workArea.Bottom - workArea.Top - 2 * marginY) / scaleY);
            _host.UpdateLayout();
            if (!GetWindowRect(_source.Handle, out var bounds)) return;
            SetWindowPos(_source.Handle, new IntPtr(-1),
                Math.Max(workArea.Left, workArea.Right - marginX - (bounds.Right - bounds.Left)),
                workArea.Top + marginY, 0, 0, ShowWithoutActivationOrResize);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_desktopHost, this)) _desktopHost = null;
            _layoutOperation?.Abort();
            _layoutOperation = null;
            _pending.Clear();
            _host.Loaded -= HostLoaded;
            _host.SizeChanged -= HostSizeChanged;
            _host.ItemsChanged -= HostItemsChanged;
            _host.Dispatcher.ShutdownStarted -= DispatcherShutdown;
            _host.ClearItems();
            _source.RemoveHook(WindowProc);
            _source.RootVisual = null;
            _source.Dispose();
        }

        private static NativeRect GetWorkArea()
        {
            var monitor = MonitorFromPoint(new NativePoint(), 1);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return info.WorkArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int Horizontal;
            public int Vertical;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int left, int top,
            int width, int height, uint flags);
    }
}
