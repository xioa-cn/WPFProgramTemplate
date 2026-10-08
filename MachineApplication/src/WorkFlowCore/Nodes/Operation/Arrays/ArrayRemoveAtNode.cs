using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("删除数组元素")]
[XTNode("数组操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "删除指定索引元素并输出新数组。")]
public sealed class ArrayRemoveAtNode : ArrayOperationNode
{
    public ArrayRemoveAtNode() : base("删除数组元素", typeof(object[]))
    {
        IndexInput = AddInput("索引", () => Index);
    }

    [XTNodeProperty("索引", "从 0 开始，不能超出数组范围。")]
    public int Index { get; set; }

    public XTNodeOption IndexInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var array = OperationValues.Array(values[0]);
        var index = OperationValues.Index(values[1], array.Length);
        return array.Where((item, position) => position != index).ToArray();
    }
}
