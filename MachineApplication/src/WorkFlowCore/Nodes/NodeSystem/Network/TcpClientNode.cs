using System.ComponentModel;
using System.Net.Sockets;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 客户端")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "单次连接、发送并接收，结束后关闭连接；读取指定长度或直到对端关闭。")]
public sealed class TcpClientNode : SocketNode
{
    public TcpClientNode() : base("TCP 客户端") { }
    [XTNodeProperty("主机", "目标 IP 地址或主机名。")]
    public string Host { get; set; } = "127.0.0.1";
    [XTNodeProperty("端口", "目标端口，1 到 65535。")]
    public int Port { get; set; } = 9000;
    [XTNodeProperty("接收响应", "关闭时只发送，输出空字节数组。")]
    public bool ReceiveResponse { get; set; } = true;

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ValidatePort(Port);
    }

    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        using var client = new TcpClient();
        await client.ConnectAsync(Host, Port, timeout.Token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Payload(values[0]), timeout.Token).ConfigureAwait(false);
        return ReceiveResponse ? await ReadResponseAsync(stream, ReceiveBytes, timeout.Token).ConfigureAwait(false) : Array.Empty<byte>();
    }
}
