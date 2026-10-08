using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("数组长度")]
[XTNode("数组操作", "xioa", "", "", "返回一维数组或列表的元素数量。")]
public sealed class ArrayCountNode : ArrayOperationNode
{
    public ArrayCountNode() : base("数组长度", typeof(int)) { }
    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Array(values[0]).Length;
}
