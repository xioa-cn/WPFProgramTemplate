using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("等于")]
[XTNode("逻辑比较", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "数字跨类型比较，字符串区分大小写；null 等于 null，集合按引用比较。")]
public sealed class EqualToNode : BinaryValueNode
{
    public EqualToNode() : base("等于") { }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) =>
        OperationValues.Equal(values[0], values[1]);
}
