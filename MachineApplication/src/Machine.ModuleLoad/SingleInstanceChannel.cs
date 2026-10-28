using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Machine.ModuleLoad;

internal sealed class SingleInstanceChannel : IDisposable {
    private readonly NamedPipeServerStream _server;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _listener;
    private int _disposed;

    public SingleInstanceChannel(string pipeName, Action activate) {
        _server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _listener = Task.Run(() => ListenAsync(activate));
    }

    public static async Task<bool> NotifyAsync(string pipeName) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try {
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var processId = new byte[sizeof(int)];
            await client.ReadExactlyAsync(processId, timeout.Token).ConfigureAwait(false);
            AllowSetForegroundWindow(BinaryPrimitives.ReadInt32LittleEndian(processId));
            await client.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
            var response = new byte[1];
            await client.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            return response[0] == 1;
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or UnauthorizedAccessException) {
            Trace.TraceWarning("单实例激活请求失败：{0}", error.Message);
            return false;
        }
    }

    private async Task ListenAsync(Action activate) {
        try {
            while (!_shutdown.IsCancellationRequested) {
                await _server.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                try {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    var processId = new byte[sizeof(int)];
                    BinaryPrimitives.WriteInt32LittleEndian(processId, Environment.ProcessId);
                    await _server.WriteAsync(processId, timeout.Token).ConfigureAwait(false);
                    var request = new byte[1];
                    await _server.ReadExactlyAsync(request, timeout.Token).ConfigureAwait(false);
                    if (request[0] == 1) {
                        activate();
                        await _server.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception error) when (error is IOException or OperationCanceledException) {
                    if (!_shutdown.IsCancellationRequested)
                        Trace.TraceWarning("单实例激活连接中断：{0}", error.Message);
                }
                finally {
                    if (!_shutdown.IsCancellationRequested && _server.IsConnected) _server.Disconnect();
                }
            }
        }
        catch (Exception error) when (_shutdown.IsCancellationRequested &&
            error is OperationCanceledException or ObjectDisposedException or IOException) {
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _shutdown.Cancel();
        _server.Dispose();
        _ = _listener.ContinueWith(completed => {
            if (completed.Exception is { } error) Trace.TraceWarning("单实例监听已停止：{0}", error);
            _shutdown.Dispose();
        }, TaskScheduler.Default);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
