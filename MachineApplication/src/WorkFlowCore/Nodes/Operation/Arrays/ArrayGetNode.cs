using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("读取数组元素")]
[XTNode("数组操作", "xioa", "", "", "按索引读取数组元素，索引从 0 开始。")]
public sealed class ArrayGetNode : ArrayOperationNode
{
    public ArrayGetNode() : base("读取数组元素", typeof(object))
    {
        IndexInput = AddInput("索引", () => Index);
    }

    [XTNodeProperty("索引", "从 0 开始，不能超出数组范围。")]
    public int Index { get; set; }

    public XTNodeOption IndexInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var array = OperationValues.Array(values[0]);
        return array[OperationValues.Index(values[1], array.Length)];
    }
}
