using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("截取数组")]
[XTNode("数组操作", "xioa", "", "", "按起始位置和长度截取数组，长度 -1 表示到末尾。")]
public sealed class ArraySliceNode : ArrayOperationNode
{
    public ArraySliceNode() : base("截取数组", typeof(object[]))
    {
        StartInput = AddInput("起始位置", () => Start);
        LengthInput = AddInput("长度", () => Length);
    }

    [XTNodeProperty("起始位置", "从 0 开始，可以等于数组长度以取得空数组。")]
    public int Start { get; set; }

    [XTNodeProperty("长度", "-1 表示到末尾；其余必须非负且不越界。")]
    public int Length { get; set; } = -1;

    public XTNodeOption StartInput { get; }
    public XTNodeOption LengthInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var array = OperationValues.Array(values[0]);
        var start = OperationValues.Index(values[1], array.Length, true);
        var length = OperationValues.Integer(values[2]);
        if (length == -1) length = array.Length - start;
        if (length < 0 || length > array.Length - start)
            throw new ArgumentOutOfRangeException(nameof(Length), "截取长度超出范围。");
        return array.Skip(start).Take(length).ToArray();
    }
}
