using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public abstract class UnaryValueNode : OperationNode
{
    protected UnaryValueNode(string title, Type outputType) : base(title, outputType)
    {
        ValueInput = AddInput("值", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("值", "未连接时使用，例如 null、123、true 或 \"文本\"；连接可接收任意对象。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }
}
