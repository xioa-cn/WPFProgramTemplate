using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("反转数组")]
[XTNode("数组操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "输出顺序反转后的数组副本。")]
public sealed class ArrayReverseNode : ArrayOperationNode
{
    public ArrayReverseNode() : base("反转数组", typeof(object[])) { }
    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Array(values[0]).Reverse().ToArray();
}
