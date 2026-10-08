using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("Base64 编码")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "Base64 编码；使用固定文化，输入无效时返回节点错误。")]
public sealed class ConvertToBase64StringNode : BytesNode
{
    public ConvertToBase64StringNode() : base("Base64 编码", typeof(string))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Convert.ToBase64String(SystemValues.Bytes(values[0]));
    }
}
