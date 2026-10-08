using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("清空数组")]
[XTNode("数组操作", "xioa", "", "", "输出空数组，不修改上游集合。")]
public sealed class ArrayClearNode : ArrayOperationNode
{
    public ArrayClearNode() : base("清空数组", typeof(object[])) { }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        _ = OperationValues.Array(values[0]);
        return Array.Empty<object>();
    }
}
