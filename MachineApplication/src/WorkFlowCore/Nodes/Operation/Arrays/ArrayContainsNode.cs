using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("数组包含元素")]
[XTNode("数组操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "判断数组是否包含指定元素。")]
public sealed class ArrayContainsNode : ArrayOperationNode
{
    public ArrayContainsNode() : base("数组包含元素", typeof(bool))
    {
        ValueInput = AddInput("元素", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("元素（JSON）", "数字支持跨类型相等，null 可直接比较。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Array(values[0]).Any(item => OperationValues.Equal(item, values[1]));
}
