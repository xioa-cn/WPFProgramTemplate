using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("获取类型")]
[XTNode("类型操作", "xioa", "", "", "获取类型；支持默认 JSON 值和连接输入，获取 null 的类型返回 null。")]
public sealed class GetTypeNode : UnaryValueNode
{
    public GetTypeNode() : base("获取类型", typeof(Type)) { }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => values[0]?.GetType();
}
