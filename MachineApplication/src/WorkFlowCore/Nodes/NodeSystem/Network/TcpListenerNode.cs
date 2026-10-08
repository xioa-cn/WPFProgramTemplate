using System.ComponentModel;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 监听接收")]
[ST.Library.UI.NodeEditor.XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "接受一个连接并接收数据，不回复；接收后或超时关闭监听。0 长度模式要求对端关闭发送方向。")]
public sealed class TcpListenerNode : TcpAcceptNode
{
    public TcpListenerNode() : base("TCP 监听接收") { }
    protected internal override bool IsPropertyVisible(string propertyName) => propertyName != nameof(SendText) && base.IsPropertyVisible(propertyName);
    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken) =>
        await AcceptAsync(values, false, cancellationToken).ConfigureAwait(false);
}
