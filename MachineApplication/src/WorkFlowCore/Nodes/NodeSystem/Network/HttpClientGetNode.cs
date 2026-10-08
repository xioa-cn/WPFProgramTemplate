using System.ComponentModel;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("HTTP GET")]
[ST.Library.UI.NodeEditor.XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "发送单次 GET，输出响应文本；非成功状态、超时或超限返回错误。")]
public sealed class HttpClientGetNode : HttpNode
{
    public HttpClientGetNode() : base("HTTP GET") { }
    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken) =>
        await RequestAsync(values, null, null, cancellationToken).ConfigureAwait(false);
}
