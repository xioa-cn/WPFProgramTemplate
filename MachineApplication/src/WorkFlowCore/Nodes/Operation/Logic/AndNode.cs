using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("逻辑与")]
[XTNode("逻辑比较", "xioa", "", "", "严格 Boolean 逻辑运算，两个输入都必须就绪。")]
public sealed class AndNode : BinaryValueNode
{
    public AndNode() : base("逻辑与") { LeftJson = "false"; RightJson = "false"; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) =>
        OperationValues.Boolean(values[0]) & OperationValues.Boolean(values[1]);
}
