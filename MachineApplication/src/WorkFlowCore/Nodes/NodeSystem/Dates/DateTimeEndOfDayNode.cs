using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("当天结束时间")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "当天结束时间；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeEndOfDayNode : DateValueNode
{
    public DateTimeEndOfDayNode() : base("当天结束时间", typeof(DateTime))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return new DateTime(SystemValues.Date(values[0]).Date.Ticks + TimeSpan.TicksPerDay - 1, SystemValues.Date(values[0]).Kind);
    }
}
