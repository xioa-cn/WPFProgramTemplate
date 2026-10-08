using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public abstract class ArrayOperationNode : OperationNode
{
    protected ArrayOperationNode(string title, Type outputType) : base(title, outputType)
    {
        ArrayInput = AddInput("数组", () => OperationValues.Parse(ItemsJson));
    }

    [XTNodeProperty("数组（JSON）", "未连接时使用的 JSON 数组，例如 [1,2,3]；修改操作输出副本，不改变上游集合。")]
    public string ItemsJson { get; set; } = "[1,2,3]";

    public XTNodeOption ArrayInput { get; }
}
