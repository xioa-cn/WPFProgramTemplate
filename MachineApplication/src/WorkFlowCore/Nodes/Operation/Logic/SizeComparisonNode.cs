using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("大小比较")]
[XTNode("逻辑比较", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "支持数字、序号字符串、DateTime 比较；不支持 null 或不兼容类型。")]
public sealed class SizeComparisonNode : BinaryValueNode
{
    public SizeComparisonNode() : base("大小比较") { }

    [XTNodeProperty("比较方式", "选择左值与右值的比较关系：大于、大于等于、小于或小于等于。")]
    public ComparisonMode Mode { get; set; } = ComparisonMode.Greater;

    protected override void ValidateSettings()
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
    }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var comparison = OperationValues.Compare(values[0], values[1]);
        return Mode switch
        {
            ComparisonMode.Greater => comparison > 0,
            ComparisonMode.GreaterOrEqual => comparison >= 0,
            ComparisonMode.Less => comparison < 0,
            ComparisonMode.LessOrEqual => comparison <= 0,
            _ => throw new ArgumentOutOfRangeException(nameof(Mode))
        };
    }
}

public enum ComparisonMode
{
    [Description("大于")]
    Greater,
    [Description("大于等于")]
    GreaterOrEqual,
    [Description("小于")]
    Less,
    [Description("小于等于")]
    LessOrEqual
}
