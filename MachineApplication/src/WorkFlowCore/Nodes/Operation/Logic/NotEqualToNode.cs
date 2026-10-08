using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("不等于")]
[XTNode("逻辑比较", "xioa", "", "", "数字跨类型比较，字符串区分大小写；null 等于 null，集合按引用比较。")]
public sealed class NotEqualToNode : BinaryValueNode
{
    public NotEqualToNode() : base("不等于") { }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) =>
        !OperationValues.Equal(values[0], values[1]);
}
