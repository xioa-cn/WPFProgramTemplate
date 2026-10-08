using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("设置数组元素")]
[XTNode("数组操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "替换指定索引处的元素并输出副本。")]
public sealed class ArraySetNode : ArrayOperationNode
{
    public ArraySetNode() : base("设置数组元素", typeof(object[]))
    {
        IndexInput = AddInput("索引", () => Index);
        ValueInput = AddInput("新值", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("索引", "从 0 开始，不能超出数组范围。")]
    public int Index { get; set; }

    [XTNodeProperty("新值（JSON）", "例如 123、null 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption IndexInput { get; }
    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var array = OperationValues.Array(values[0]);
        array[OperationValues.Index(values[1], array.Length)] = values[2];
        return array;
    }
}
