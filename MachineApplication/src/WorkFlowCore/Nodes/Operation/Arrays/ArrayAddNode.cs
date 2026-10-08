using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("追加数组元素")]
[XTNode("数组操作", "xioa", "", "", "追加元素并输出新数组，不修改上游集合。")]
public sealed class ArrayAddNode : ArrayOperationNode
{
    public ArrayAddNode() : base("追加数组元素", typeof(object[]))
    {
        ValueInput = AddInput("元素", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("元素（JSON）", "例如 123、null 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Array(values[0]).Append(values[1]).ToArray();
}
