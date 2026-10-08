using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("查找数组元素")]
[XTNode("数组操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "返回元素首次出现的索引，未找到返回 -1。")]
public sealed class ArrayIndexOfNode : ArrayOperationNode
{
    public ArrayIndexOfNode() : base("查找数组元素", typeof(int))
    {
        ValueInput = AddInput("元素", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("元素（JSON）", "数字支持跨类型相等，null 可直接比较。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => Array.FindIndex(OperationValues.Array(values[0]), item => OperationValues.Equal(item, values[1]));
}
