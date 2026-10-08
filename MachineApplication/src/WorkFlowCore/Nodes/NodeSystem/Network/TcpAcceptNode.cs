using System.Net.Sockets;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class TcpAcceptNode : SocketNode
{
    protected TcpAcceptNode(string title) : base(title) { }
    [XTNodeProperty("监听地址", "默认仅本机；0.0.0.0 表示所有 IPv4 网卡。")]
    public string BindAddress { get; set; } = "127.0.0.1";
    [XTNodeProperty("监听端口", "1 到 65535；监听接收节点处理单次连接，TCP 服务端持续监听直到停止。")]
    public int Port { get; set; } = 9000;
    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        _ = LocalAddress(BindAddress);
        ValidatePort(Port);
    }

    protected async Task<byte[]> AcceptAsync(object?[] values, bool reply, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        var listener = new TcpListener(LocalAddress(BindAddress), Port);
        try
        {
            listener.Start();
            using var client = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            var received = await ReadResponseAsync(stream, ReceiveBytes, timeout.Token).ConfigureAwait(false);
            if (reply) await stream.WriteAsync(Payload(values[0]), timeout.Token).ConfigureAwait(false);
            return received;
        }
        finally { listener.Stop(); }
    }
}
