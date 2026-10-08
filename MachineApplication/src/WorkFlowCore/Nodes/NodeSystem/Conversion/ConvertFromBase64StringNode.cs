using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("Base64 解码")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "Base64 解码；使用固定文化，输入无效时返回节点错误。")]
public sealed class ConvertFromBase64StringNode : TextValueNode
{
    public ConvertFromBase64StringNode() : base("Base64 解码", typeof(byte[]))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Convert.FromBase64String(SystemValues.Text(values[0]));
    }
}
