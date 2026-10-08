using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("解析 GUID")]
[XTNode("GUID 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "解析 GUID；使用固定文化，输入无效时返回节点错误。")]
public sealed class GuidParseNode : TextValueNode
{
    public GuidParseNode() : base("解析 GUID", typeof(Guid))
    {
        Text = "00000000-0000-0000-0000-000000000000";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return System.Guid.Parse(SystemValues.Text(values[0]));
    }
}
