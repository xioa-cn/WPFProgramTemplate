using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("逻辑或")]
[XTNode("逻辑比较", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "严格 Boolean 逻辑运算，两个输入都必须就绪。")]
public sealed class OrNode : BinaryValueNode
{
    public OrNode() : base("逻辑或") { LeftJson = "false"; RightJson = "false"; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) =>
        OperationValues.Boolean(values[0]) | OperationValues.Boolean(values[1]);
}
