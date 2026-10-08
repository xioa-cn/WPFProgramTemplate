using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class SocketNode : NetworkNode
{
    protected SocketNode(string title) : base(title, typeof(byte[]))
    {
        SendInput = AddInput("发送数据", () => SendText);
    }

    [XTNodeProperty("发送文本", "未连接时发送 UTF-8 文本；发送端口也接受 byte[]，不会自动追加换行。")]
    public string SendText { get; set; } = string.Empty;
    [XTNodeProperty("接收长度", "TCP：0 表示读取至对端关闭，正数表示读取指定字节数；服务端协议须正确配置。")]
    public int ReceiveBytes { get; set; }
    public XTNodeOption SendInput { get; }

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        if (ReceiveBytes < 0 || ReceiveBytes > MaxResponseBytes) throw new ArgumentOutOfRangeException(nameof(ReceiveBytes));
    }

    protected static byte[] Payload(object? value) => value is byte[] bytes ? bytes
        : SystemValues.Encoding(TextEncodingKind.Utf8).GetBytes(SystemValues.Text(value));
    protected override void ValidateValues(object?[] values) => _ = Payload(values[0]);
}
