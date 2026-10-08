using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("今天日期")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "今天日期；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeTodayNode : SystemNode
{
    public DateTimeTodayNode() : base("今天日期", typeof(DateTime))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return DateTime.Today;
    }
}
