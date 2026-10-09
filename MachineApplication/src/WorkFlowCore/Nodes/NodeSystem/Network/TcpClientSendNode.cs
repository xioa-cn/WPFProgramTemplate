using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 客户端发送")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "通过 Key 查找已连接的 TCP 客户端发送数据；断线期间明确失败，不自动重放消息。")]
public sealed class TcpClientSendNode : TcpSendNode
{
    public TcpClientSendNode() : base("TCP 客户端发送") { Key = "tcpClient1"; }
    protected override bool IsServer => false;
}
