using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class ValueNode : SystemNode
{
    protected ValueNode(string title, Type outputType) : base(title, outputType)
    {
        ValueInput = AddInput("值", () => SystemValues.JsonValue(ValueJson));
    }

    [XTNodeProperty("值（JSON）", "未连接时使用，如 123、true、null 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }
}
