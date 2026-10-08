using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public enum UdpOperation { Send, Receive, SendAndReceive }

[DisplayName("UDP 收发")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "发送一个数据报或接收一个数据报，操作结束即释放套接字。")]
public sealed class UdpClientNode : SocketNode
{
    public UdpClientNode() : base("UDP 收发") { }
    [XTNodeProperty("操作", "Send 仅发送，Receive 仅接收，SendAndReceive 发送并接收回复。")]
    public UdpOperation Mode { get; set; } = UdpOperation.SendAndReceive;
    [XTNodeProperty("目标主机", "发送目标 IP 或主机名。")]
    public string Host { get; set; } = "127.0.0.1";
    [XTNodeProperty("目标端口", "发送目标端口。")]
    public int Port { get; set; } = 9000;
    [XTNodeProperty("本地地址", "本地绑定 IP，默认仅本机。")]
    public string BindAddress { get; set; } = "127.0.0.1";
    [XTNodeProperty("本地端口", "发送时 0 表示自动分配；仅接收模式必须指定端口。")]
    public int LocalPort { get; set; }
    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
        _ = LocalAddress(BindAddress);
        if (LocalPort is < 0 or > 65535 || Mode == UdpOperation.Receive && LocalPort == 0)
            throw new ArgumentException("本地端口无效。");
        if (Mode != UdpOperation.Receive)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Host);
            ValidatePort(Port);
        }
    }
    protected internal override bool IsPropertyVisible(string propertyName) => propertyName != nameof(ReceiveBytes) && base.IsPropertyVisible(propertyName);
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        if (Payload(values[0]).Length > 65507) throw new ArgumentException("UDP 数据报过大。");
    }
    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        using var client = new UdpClient(new IPEndPoint(LocalAddress(BindAddress), LocalPort));
        if (Mode != UdpOperation.Receive)
        {
            var addresses = await Dns.GetHostAddressesAsync(Host, timeout.Token).ConfigureAwait(false);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == client.Client.AddressFamily)
                ?? throw new ArgumentException("目标地址与本地地址的 IP 版本不匹配。");
            client.Connect(address, Port);
            await client.SendAsync(Payload(values[0]), timeout.Token).ConfigureAwait(false);
        }
        if (Mode == UdpOperation.Send) return Array.Empty<byte>();
        var result = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
        if (result.Buffer.Length > MaxResponseBytes) throw new InvalidOperationException("UDP 响应超出接收限制。");
        return result.Buffer;
    }
}
