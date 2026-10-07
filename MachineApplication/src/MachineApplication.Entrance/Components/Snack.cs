using Machine.ModuleLoad.Region;
using Machine.ModuleLoad.Logger;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace MachineApplication.Entrance.Components;

public class Snack : Snackbar, ISnackBar, IDisposable
{
    private readonly SnackbarMessageQueue _messageQueue;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public Snack()
    {
        _messageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        MessageQueue = _messageQueue;
        SetResourceReference(StyleProperty, typeof(Snackbar));
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(16);
        Focusable = false;
        Panel.SetZIndex(this, 6000);
    }

    /// <summary>将消息加入显示队列；允许从后台线程调用。</summary>
    /// <param name="message">非空消息文本。</param>
    /// <param name="duration">显示时长，单位为毫秒，必须大于零。</param>
    public void SendMessage(string message, int duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

            _messageQueue.Enqueue(message,
                actionContent: null,
                actionHandler: null,
                actionArgument: null,
                promote: false,
                neverConsiderToBeDuplicate: true,
                durationOverride: TimeSpan.FromMilliseconds(duration));
            // GlobalLogger.DebuggerLogger?.Info($"[Snack] {message} (duration: {duration} ms)");
            GlobalLogger.DebuggerLogger?.Info($"Snack".ClaLog($"{message} (duration: {duration} ms)"));
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            _messageQueue.Clear();
            _messageQueue.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}