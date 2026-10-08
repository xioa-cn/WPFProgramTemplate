using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("比较日期时间")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "比较日期时间；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeCompareNode : DateValueNode
{
    public DateTimeCompareNode() : base("比较日期时间", typeof(int))
    {
        OtherInput = AddInput("另一时间", () => OtherDate);
    }

    [XTNodeProperty("另一时间", "返回 -1、0、1；比较 DateTime 刻度，不隐式转换时区。")]
    public string OtherDate { get; set; } = "2000-01-01T00:00:00";
    public XTNodeOption OtherInput { get; }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        _ = SystemValues.Date(values[1]);
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return DateTime.Compare(SystemValues.Date(values[0]), SystemValues.Date(values[1]));
    }
}
