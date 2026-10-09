using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

internal interface ITcpConnection
{
    bool IsRunning { get; }
    bool IsConnected { get; }
    void Start();
    Task StopAsync();
    Task SendAsync(byte[] data, string clientId, CancellationToken cancellationToken);
}

internal sealed class TcpConnectionRegistry
{
    private static readonly ConditionalWeakTable<XTNodeEditor, TcpConnectionRegistry> Registries = new();
    private readonly ConcurrentDictionary<string, ITcpConnection> _connections = new(StringComparer.OrdinalIgnoreCase);

    public static TcpConnectionRegistry For(XTNodeEditor editor) => Registries.GetValue(editor, _ => new());

    public void Register(string key, ITcpConnection connection)
    {
        if (!_connections.TryAdd(key, connection))
            throw new InvalidOperationException($"TCP Key“{key}”已被占用，请使用不同名称。");
    }

    public void Remove(string key, ITcpConnection connection) =>
        ((ICollection<KeyValuePair<string, ITcpConnection>>)_connections).Remove(new(key, connection));

    public ITcpConnection Get(string key, bool server)
    {
        if (!_connections.TryGetValue(key, out var connection))
            throw new InvalidOperationException($"未找到 TCP Key“{key}”，请先启动对应连接节点。");
        if ((connection is PersistentTcpServer) != server)
            throw new InvalidOperationException($"TCP Key“{key}”的连接类型与发送节点不匹配。");
        return connection;
    }
}

internal sealed record TcpConnectionOptions(int ReceiveBytes, int MaxResponseBytes, int TimeoutMilliseconds,
    bool AutoReconnect, int ReconnectIntervalMilliseconds);
