using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 服务端")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按 Key 提供持久监听和接收；不自动回复，使用 TCP 服务端发送节点发送数据。")]
public sealed class TcpServerNode : TcpConnectionNode
{
    public TcpServerNode() : base("TCP 服务端")
    {
        ReceiveBytes = 1;
        ClientIdOutput = OutputOptions.Add("客户端 ID", typeof(string), false);
    }

    [XTNodeProperty("监听地址", "默认仅本机；0.0.0.0 表示所有 IPv4 网卡。")]
    public string BindAddress { get; set; } = "127.0.0.1";

    [XTNodeProperty("监听端口", "1 到 65535。")]
    public int Port { get; set; } = 9000;

    [XTNodeProperty("最大客户端数", "同时保持连接的客户端上限，1 到 1024；超出时关闭新连接。")]
    public int MaxClients { get; set; } = 32;

    public XTNodeOption ClientIdOutput { get; }
    public bool IsListening => IsConnected;
    public int ClientCount => (Connection as PersistentTcpServer)?.ClientCount ?? 0;

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        _ = LocalAddress(BindAddress);
        ValidatePort(Port);
        if (MaxClients is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaxClients));
    }

    private protected override object GetSettings() => new TcpServerSettings(LocalAddress(BindAddress), Port, MaxClients, Options);

    private protected override ITcpConnection CreateConnection(object settings, Action<byte[], string> received,
        Action<string> reportState) => new PersistentTcpServer((TcpServerSettings)settings, received, reportState);

    protected override void PublishClientId(string clientId) => ClientIdOutput.TransferData(clientId);

    public void StopServer() => Stop();
    public Task StopServerAsync() => StopAsync();
}
