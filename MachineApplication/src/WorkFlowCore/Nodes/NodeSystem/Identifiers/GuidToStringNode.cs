using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("GUID 转字符串")]
[XTNode("GUID 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "GUID 转字符串；使用固定文化，输入无效时返回节点错误。")]
public sealed class GuidToStringNode : TextValueNode
{
    public GuidToStringNode() : base("GUID 转字符串", typeof(string))
    {
        Text = "00000000-0000-0000-0000-000000000000";
    }

    [XTNodeProperty("格式", "N、D、B、P 或 X。")]
    public string Format { get; set; } = "D";
    protected override void ValidateSettings() => _ = System.Guid.Empty.ToString(Format);
    protected override void ValidateValues(object?[] values)
    {
        if (values[0] is not System.Guid) _ = System.Guid.Parse(SystemValues.Text(values[0]));
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return (values[0] is Guid guid ? guid : System.Guid.Parse(SystemValues.Text(values[0]))).ToString(Format);
    }
}
