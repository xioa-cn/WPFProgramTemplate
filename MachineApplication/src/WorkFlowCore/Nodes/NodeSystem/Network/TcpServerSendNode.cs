using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 服务端发送")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "通过 Key 查找服务端；客户端 ID 为空时广播，否则仅向指定客户端发送。")]
public sealed class TcpServerSendNode : TcpSendNode
{
    public TcpServerSendNode() : base("TCP 服务端发送")
    {
        ClientIdInput = AddInput("客户端 ID", () => ClientId);
    }

    [XTNodeProperty("客户端 ID", "留空广播；可连接服务端的客户端 ID 输出，以回复当前收到数据的客户端。")]
    public string ClientId { get; set; } = string.Empty;

    public XTNodeOption ClientIdInput { get; }
    protected override bool IsServer => true;
    protected override string GetClientId(object?[] values) => SystemValues.Text(values[1]).Trim();
}
