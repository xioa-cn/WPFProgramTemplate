using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取时间刻度")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取时间刻度；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeGetTicksNode : DateValueNode
{
    public DateTimeGetTicksNode() : base("获取时间刻度", typeof(long))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return SystemValues.Date(values[0]).Ticks;
    }
}
