using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace WorkFlowCore.Nodes.NodeSystem;

internal sealed record TcpServerSettings(IPAddress Address, int Port, int MaxClients, TcpConnectionOptions Options);

internal sealed class PersistentTcpServer(TcpServerSettings settings, Action<byte[], string> received,
    Action<string> reportState) : ITcpConnection
{
    private sealed class Client(TcpClient client)
    {
        public TcpPeer Peer { get; } = new(client);
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly object _gate = new();
    private TcpListener? _listener;
    private Task _completion = Task.CompletedTask;
    private volatile bool _listening;

    public bool IsRunning => !_shutdown.IsCancellationRequested && !_completion.IsCompleted;
    public bool IsConnected => _listening;
    public int ClientCount => _clients.Count;

    public void Start() => _completion = Task.Run(RunAsync);

    public Task StopAsync()
    {
        _shutdown.Cancel();
        _listening = false;
        lock (_gate) _listener?.Stop();
        foreach (var client in _clients.Values) client.Peer.Dispose();
        return _completion;
    }

    public async Task SendAsync(byte[] data, string clientId, CancellationToken cancellationToken)
    {
        if (!_listening) throw new InvalidOperationException("TCP 服务端尚未开始监听。");
        var clients = string.IsNullOrWhiteSpace(clientId) ? _clients.Values.ToArray()
            : _clients.TryGetValue(clientId, out var client) ? [client]
            : throw new InvalidOperationException($"TCP 客户端“{clientId}”未连接或已经断开。");
        if (clients.Length == 0) throw new InvalidOperationException("TCP 服务端没有已连接的客户端，消息未发送。");
        await Task.WhenAll(clients.Select(client => client.Peer.SendAsync(data, cancellationToken))).ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                using var session = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                var listener = new TcpListener(settings.Address, settings.Port);
                try
                {
                    lock (_gate)
                    {
                        _shutdown.Token.ThrowIfCancellationRequested();
                        _listener = listener;
                        listener.Start();
                        _listening = true;
                    }
                    reportState($"监听中 · {settings.Address}:{settings.Port}");
                    while (!session.IsCancellationRequested)
                    {
                        var socket = await listener.AcceptTcpClientAsync(session.Token).ConfigureAwait(false);
                        if (session.IsCancellationRequested || _clients.Count >= settings.MaxClients)
                        {
                            socket.Dispose();
                            continue;
                        }
                        var clientId = Guid.NewGuid().ToString("N");
                        var client = new Client(socket);
                        _clients[clientId] = client;
                        _ = ReceiveClientAsync(clientId, client, session.Token);
                    }
                }
                catch (Exception exception) when (!_shutdown.IsCancellationRequested)
                {
                    reportState($"TCP 监听失败：{exception.Message}");
                }
                finally
                {
                    _listening = false;
                    session.Cancel();
                    lock (_gate) { listener.Stop(); _listener = null; }
                    var clients = _clients.Values.ToArray();
                    foreach (var client in clients) client.Peer.Dispose();
                    await Task.WhenAll(clients.Select(client => client.Completion.Task)).ConfigureAwait(false);
                }
                if (!settings.Options.AutoReconnect) break;
                reportState("等待自动恢复监听…");
                await Task.Delay(settings.Options.ReconnectIntervalMilliseconds, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (_shutdown.IsCancellationRequested &&
            exception is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }

    private async Task ReceiveClientAsync(string clientId, Client client, CancellationToken cancellationToken)
    {
        try
        {
            await client.Peer.ReceiveAsync(settings.Options, bytes => received(bytes, clientId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            reportState($"TCP 客户端断开：{exception.Message}");
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
            exception is OperationCanceledException or ObjectDisposedException or System.IO.IOException or SocketException) { }
        finally
        {
            client.Peer.Dispose();
            _clients.TryRemove(clientId, out _);
            client.Completion.TrySetResult();
        }
    }
}
