using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("当前本地时间")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "当前本地时间；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeNowNode : SystemNode
{
    public DateTimeNowNode() : base("当前本地时间", typeof(DateTime))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return DateTime.Now;
    }
}
