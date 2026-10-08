using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("解析日期时间")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "解析日期时间；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeParseNode : TextValueNode
{
    public DateTimeParseNode() : base("解析日期时间", typeof(DateTime))
    {
        Text = "2000-01-01T00:00:00";
    }

    [XTNodeProperty("精确格式", "留空自动解析；例如 yyyy-MM-dd HH:mm:ss，使用固定文化。")]
    public string Format { get; set; } = string.Empty;
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(Format)
            ? SystemValues.Date(values[0])
            : DateTime.ParseExact(SystemValues.Text(values[0]), Format, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
