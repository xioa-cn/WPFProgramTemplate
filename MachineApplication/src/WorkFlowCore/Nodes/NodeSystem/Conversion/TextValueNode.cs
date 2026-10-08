using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class TextValueNode : SystemNode
{
    protected TextValueNode(string title, Type outputType) : base(title, outputType)
    {
        ValueInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "未连接时使用的原始文本，无需额外 JSON 引号。")]
    public string Text { get; set; } = string.Empty;

    public XTNodeOption ValueInput { get; }
    protected override void ValidateValues(object?[] values) => _ = SystemValues.Text(values[0]);
}
