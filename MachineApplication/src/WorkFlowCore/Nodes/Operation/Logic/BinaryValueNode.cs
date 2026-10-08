using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public abstract class BinaryValueNode : OperationNode
{
    protected BinaryValueNode(string title) : base(title, typeof(bool))
    {
        LeftInput = AddInput("左值", () => OperationValues.Parse(LeftJson));
        RightInput = AddInput("右值", () => OperationValues.Parse(RightJson));
    }

    [XTNodeProperty("左值（JSON）", "例如数字 1、文本 \"abc\"、true 或 null；连接后使用输入值。")]
    public string LeftJson { get; set; } = "0";

    [XTNodeProperty("右值（JSON）", "数字跨类型比较；字符串使用区分大小写的序号比较。")]
    public string RightJson { get; set; } = "0";

    public XTNodeOption LeftInput { get; }
    public XTNodeOption RightInput { get; }
}
