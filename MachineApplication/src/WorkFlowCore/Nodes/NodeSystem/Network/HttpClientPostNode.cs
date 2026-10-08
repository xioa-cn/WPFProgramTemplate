using System.ComponentModel;
using System.Net.Http.Headers;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("HTTP POST")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "发送单次 POST，输出响应文本；仅执行时联网。")]
public sealed class HttpClientPostNode : HttpNode
{
    public HttpClientPostNode() : base("HTTP POST") { BodyInput = AddInput("请求正文", () => Body); }
    [XTNodeProperty("请求正文", "UTF-8 编码的请求正文。")]
    public string Body { get; set; } = "{}";
    [XTNodeProperty("媒体类型", "例如 application/json 或 text/plain。")]
    public string MediaType { get; set; } = "application/json";
    public XTNodeOption BodyInput { get; }
    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        _ = new MediaTypeHeaderValue(MediaType);
    }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        _ = SystemValues.Text(values[1]);
    }
    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken) =>
        await RequestAsync(values, SystemValues.Text(values[1]), MediaType, cancellationToken).ConfigureAwait(false);
}
