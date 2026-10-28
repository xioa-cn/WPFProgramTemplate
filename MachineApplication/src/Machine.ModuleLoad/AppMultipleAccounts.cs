using Microsoft.Extensions.DependencyInjection;

using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Machine.ModuleLoad;

public static class AppMultipleAccounts {
    private static readonly object SyncRoot = new();
    private static Mutex? _instanceMutex;
    private static SingleInstanceChannel? _activationChannel;
    private static Application? _application;
    private static int _activationPending;

    /// <summary>
    /// 防止程序多开，重复启动时激活已有实例的当前窗口并退出新进程。
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <param name="applicationId">由调用方指定的应用唯一标识，相同标识的程序不允许重复启动。</param>
    /// <returns></returns>
    public static IServiceCollection MultipleAccounts(this IServiceCollection serviceCollection, string applicationId) {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(applicationId)));
        var pipeName = $"MachineApplication.SingleInstance.{Process.GetCurrentProcess().SessionId}.{identifier}";
        lock (SyncRoot) {
            if (_instanceMutex is not null) return serviceCollection;

            var mutex = new Mutex(false, $@"Local\MachineApplication.SingleInstance.{identifier}", out var createdNew);
            if (createdNew) {
                try {
                    _activationChannel = new SingleInstanceChannel(pipeName, RequestActivation);
                }
                catch {
                    mutex.Dispose();
                    throw;
                }
                _instanceMutex = mutex;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                return serviceCollection;
            }

            mutex.Dispose();
        }

        var notified = SingleInstanceChannel.NotifyAsync(pipeName).GetAwaiter().GetResult();
        if (!notified) Trace.TraceWarning("无法通知已有程序恢复窗口：{0}", applicationId);
        Environment.Exit(0);
        return serviceCollection;
    }

    internal static void AttachApplication(Application application) {
        if (_instanceMutex is null) return;
        Volatile.Write(ref _application, application);
        application.Exit += OnApplicationExit;
        if (Volatile.Read(ref _activationPending) != 0) RequestActivation();
    }

    private static void RequestActivation() {
        Interlocked.Exchange(ref _activationPending, 1);
        var application = Volatile.Read(ref _application);
        if (application is null || application.Dispatcher.HasShutdownStarted) return;
        application.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {
            if (application.Dispatcher.HasShutdownStarted || application.MainWindow is not { } window) return;
            if (Interlocked.Exchange(ref _activationPending, 0) == 0) return;
            RestoreWindow(window);
        }));
    }

    private static void RestoreWindow(Window window) {
        var handle = new WindowInteropHelper(window).Handle;
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) {
            if (handle != IntPtr.Zero) ShowWindow(handle, 9);
            else window.WindowState = WindowState.Normal;
        }
        while (window.OwnedWindows.Cast<Window>().LastOrDefault(owned => owned.IsVisible) is { } ownedWindow)
            window = ownedWindow;
        handle = new WindowInteropHelper(window).EnsureHandle();
        var popup = GetLastActivePopup(handle);
        if (popup != IntPtr.Zero && IsWindowVisible(popup)) handle = popup;
        window.Activate();
        if (!SetForegroundWindow(handle)) FlashWindow(handle, true);
    }

    private static void OnApplicationExit(object sender, ExitEventArgs args) {
        Volatile.Write(ref _application, null);
        ReleaseInstance();
    }

    private static void OnProcessExit(object? sender, EventArgs args) {
        ReleaseInstance();
    }

    private static void ReleaseInstance() {
        Interlocked.Exchange(ref _activationChannel, null)?.Dispose();
        Interlocked.Exchange(ref _instanceMutex, null)?.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool invert);
}
