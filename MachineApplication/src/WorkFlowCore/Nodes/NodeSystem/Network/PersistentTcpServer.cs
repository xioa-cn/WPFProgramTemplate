using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace WorkFlowCore.Nodes.NodeSystem;

internal sealed record TcpServerSettings(IPAddress Address, int Port, int ReceiveBytes, int MaxResponseBytes,
    int TimeoutMilliseconds, int MaxClients);

internal sealed class PersistentTcpServer
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    private readonly Func<byte[], CancellationToken, Task> _received;
    private readonly Func<string, CancellationToken, Task> _reportError;
    private int _stopping;
    private Task _completion = Task.CompletedTask;

    public PersistentTcpServer(TcpServerSettings settings, byte[] reply,
        Func<byte[], CancellationToken, Task> received, Func<string, CancellationToken, Task> reportError)
    {
        Settings = settings;
        Reply = reply.ToArray();
        _received = received;
        _reportError = reportError;
        _listener = new TcpListener(settings.Address, settings.Port);
    }

    public TcpServerSettings Settings { get; }
    public byte[] Reply { get; }
    public bool IsListening => Volatile.Read(ref _stopping) == 0 && !_completion.IsCompleted;
    public Task Completion => _completion;

    public void Start()
    {
        try { _listener.Start(); }
        catch { _shutdown.Dispose(); throw; }
        _completion = Task.Run(AcceptClientsAsync);
    }

    public Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 0)
        {
            _shutdown.Cancel();
            _listener.Stop();
            foreach (var client in _clients.Keys) client.Dispose();
        }
        return _completion;
    }

    private async Task AcceptClientsAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
                if (_shutdown.IsCancellationRequested || _clients.Count >= Settings.MaxClients)
                {
                    client.Dispose();
                    continue;
                }
                var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _clients[client] = finished.Task;
                _ = ServeClientAsync(client, finished);
            }
        }
        catch (Exception exception) when (_shutdown.IsCancellationRequested &&
            exception is OperationCanceledException or SocketException or ObjectDisposedException) { }
        catch (Exception exception)
        {
            await NotifyErrorAsync($"TCP 监听失败：{exception.Message}").ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _stopping, 1);
            _shutdown.Cancel();
            _listener.Stop();
            var clients = _clients.ToArray();
            foreach (var entry in clients) entry.Key.Dispose();
            await Task.WhenAll(clients.Select(entry => entry.Value)).ConfigureAwait(false);
        }
    }

    private async Task ServeClientAsync(TcpClient client, TaskCompletionSource finished)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                while (!_shutdown.IsCancellationRequested)
                {
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    var bytes = await ReadFrameAsync(stream, request).ConfigureAwait(false);
                    if (bytes is null) return;
                    await stream.WriteAsync(Reply, request.Token).ConfigureAwait(false);
                    await _received(bytes, _shutdown.Token).ConfigureAwait(false);
                    if (Settings.ReceiveBytes == 0) return;
                }
            }
        }
        catch (Exception exception) when (_shutdown.IsCancellationRequested &&
            exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
        catch (Exception exception)
        {
            await NotifyErrorAsync(exception is OperationCanceledException ? "TCP 客户端请求超时，已关闭该连接。"
                : $"TCP 客户端断开：{exception.Message}").ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
            finished.TrySetResult();
            _clients.TryRemove(client, out _);
        }
    }

    private async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationTokenSource request)
    {
        var first = new byte[1];
        if (await stream.ReadAsync(first, _shutdown.Token).ConfigureAwait(false) == 0) return null;
        request.CancelAfter(Settings.TimeoutMilliseconds);
        if (Settings.ReceiveBytes > 0)
        {
            var result = new byte[Settings.ReceiveBytes];
            result[0] = first[0];
            await stream.ReadExactlyAsync(result.AsMemory(1), request.Token).ConfigureAwait(false);
            return result;
        }
        using var buffer = new MemoryStream();
        buffer.WriteByte(first[0]);
        var chunk = new byte[System.Math.Min(8192, Settings.MaxResponseBytes + 1)];
        while (true)
        {
            var remaining = Settings.MaxResponseBytes + 1 - (int)buffer.Length;
            var count = await stream.ReadAsync(chunk.AsMemory(0, System.Math.Min(chunk.Length, remaining)), request.Token).ConfigureAwait(false);
            if (count == 0) return buffer.ToArray();
            buffer.Write(chunk, 0, count);
            if (buffer.Length > Settings.MaxResponseBytes) throw new IOException("请求超过最大接收字节数。");
        }
    }

    private async Task NotifyErrorAsync(string message)
    {
        try { await _reportError(message, _shutdown.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }
}
