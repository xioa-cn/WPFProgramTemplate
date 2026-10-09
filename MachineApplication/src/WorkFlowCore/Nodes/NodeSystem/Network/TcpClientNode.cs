using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 客户端")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按 Key 建立持久连接并后台接收，支持自动重连；使用 TCP 客户端发送节点发送数据。")]
public sealed class TcpClientNode : TcpConnectionNode
{
    public TcpClientNode() : base("TCP 客户端") { Key = "tcpClient1"; }

    [XTNodeProperty("主机", "目标 IP 地址或主机名。")]
    public string Host { get; set; } = "127.0.0.1";

    [XTNodeProperty("端口", "目标端口，1 到 65535。")]
    public int Port { get; set; } = 9000;

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ValidatePort(Port);
    }

    private protected override object GetSettings() => new TcpClientSettings(Host.Trim(), Port, Options);

    private protected override ITcpConnection CreateConnection(object settings, Action<byte[], string> received,
        Action<string> reportState) => new PersistentTcpClient((TcpClientSettings)settings, received, reportState);

    public void StopClient() => Stop();
    public Task StopClientAsync() => StopAsync();
}
