using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class BytesNode : SystemNode
{
    protected BytesNode(string title, Type outputType) : base(title, outputType)
    {
        BytesInput = AddInput("字节数组", () => BytesJson);
    }

    [XTNodeProperty("字节数组（JSON）", "默认字节序列，例如 [0,127,255]，每项为 0 到 255 的整数。")]
    public string BytesJson { get; set; } = "[]";
    public XTNodeOption BytesInput { get; }
    protected override void ValidateValues(object?[] values) => _ = SystemValues.Bytes(values[0]);
}
