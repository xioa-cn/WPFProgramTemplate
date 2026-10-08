using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("逻辑非")]
[XTNode("逻辑比较", "xioa", "", "", "逻辑非；支持默认 JSON 值和连接输入，获取 null 的类型返回 null。")]
public sealed class NotNode : UnaryValueNode
{
    public NotNode() : base("逻辑非", typeof(bool)) { ValueJson = "false"; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => !OperationValues.Boolean(values[0]);
}
