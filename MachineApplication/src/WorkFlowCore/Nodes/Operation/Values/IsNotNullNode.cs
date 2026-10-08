using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("是否非 null")]
[XTNode("类型操作", "xioa", "", "", "是否非 null；支持默认 JSON 值和连接输入，获取 null 的类型返回 null。")]
public sealed class IsNotNullNode : UnaryValueNode
{
    public IsNotNullNode() : base("是否非 null", typeof(bool)) { }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => values[0] is not null;
}
