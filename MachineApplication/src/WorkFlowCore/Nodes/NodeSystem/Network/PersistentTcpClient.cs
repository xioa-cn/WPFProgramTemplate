using System.Net.Sockets;

namespace WorkFlowCore.Nodes.NodeSystem;

internal sealed record TcpClientSettings(string Host, int Port, TcpConnectionOptions Options);

internal sealed class PersistentTcpClient(TcpClientSettings settings, Action<byte[], string> received,
    Action<string> reportState) : ITcpConnection
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private TcpClient? _client;
    private TcpPeer? _peer;
    private Task _completion = Task.CompletedTask;

    public bool IsRunning => !_shutdown.IsCancellationRequested && !_completion.IsCompleted;
    public bool IsConnected { get { lock (_gate) return _peer is not null; } }

    public void Start() => _completion = Task.Run(RunAsync);

    public Task StopAsync()
    {
        _shutdown.Cancel();
        lock (_gate) { _peer = null; _client?.Dispose(); }
        return _completion;
    }

    public Task SendAsync(byte[] data, string clientId, CancellationToken cancellationToken)
    {
        TcpPeer peer;
        lock (_gate) peer = _peer ?? throw new InvalidOperationException("TCP 客户端尚未连接或正在重连，消息未发送。");
        return peer.SendAsync(data, cancellationToken);
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                using var client = new TcpClient();
                lock (_gate)
                {
                    _shutdown.Token.ThrowIfCancellationRequested();
                    _client = client;
                }
                try
                {
                    reportState($"正在连接 · {settings.Host}:{settings.Port}");
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    timeout.CancelAfter(settings.Options.TimeoutMilliseconds);
                    await client.ConnectAsync(settings.Host, settings.Port, timeout.Token).ConfigureAwait(false);
                    using var peer = new TcpPeer(client);
                    lock (_gate)
                    {
                        _shutdown.Token.ThrowIfCancellationRequested();
                        _peer = peer;
                    }
                    reportState($"已连接 · {settings.Host}:{settings.Port}");
                    await peer.ReceiveAsync(settings.Options, bytes => received(bytes, string.Empty), _shutdown.Token)
                        .ConfigureAwait(false);
                    reportState("TCP 对端已关闭连接");
                }
                catch (Exception exception) when (!_shutdown.IsCancellationRequested)
                {
                    reportState($"TCP 连接断开或失败：{exception.Message}");
                }
                finally
                {
                    lock (_gate) { _peer = null; _client = null; }
                    client.Dispose();
                }
                if (!settings.Options.AutoReconnect) break;
                reportState("等待自动重连…");
                await Task.Delay(settings.Options.ReconnectIntervalMilliseconds, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (_shutdown.IsCancellationRequested &&
            exception is OperationCanceledException or ObjectDisposedException or System.IO.IOException or SocketException) { }
    }
}
