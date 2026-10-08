using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("创建数组")]
[XTNode("数组操作", "xioa", "", "", "从 JSON 数组或上游列表创建 object[] 副本。")]
public sealed class ArrayCreateNode : ArrayOperationNode
{
    public ArrayCreateNode() : base("创建数组", typeof(object[])) { ItemsJson = "[]"; }
    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Array(values[0]);
}
