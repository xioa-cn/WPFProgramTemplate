using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("创建日期时间")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "创建日期时间；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeCreateNode : SystemNode
{
    public DateTimeCreateNode() : base("创建日期时间", typeof(DateTime))
    {

    }

    [XTNodeProperty("年", "1 到 9999。")]
    public int Year { get; set; } = 2000;
    [XTNodeProperty("月", "1 到 12。")]
    public int Month { get; set; } = 1;
    [XTNodeProperty("日", "必须是对应年月中的有效日期。")]
    public int Day { get; set; } = 1;
    [XTNodeProperty("时", "0 到 23。")]
    public int Hour { get; set; }
    [XTNodeProperty("分", "0 到 59。")]
    public int Minute { get; set; }
    [XTNodeProperty("秒", "0 到 59。")]
    public int Second { get; set; }
    [XTNodeProperty("毫秒", "0 到 999。")]
    public int Millisecond { get; set; }
    [XTNodeProperty("时间种类", "Unspecified、Local 或 Utc。")]
    public DateTimeKind Kind { get; set; } = DateTimeKind.Unspecified;
    private DateTime Create() => new(Year, Month, Day, Hour, Minute, Second, Millisecond, Kind);
    protected override void ValidateSettings() => _ = Create();
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Create();
    }
}
