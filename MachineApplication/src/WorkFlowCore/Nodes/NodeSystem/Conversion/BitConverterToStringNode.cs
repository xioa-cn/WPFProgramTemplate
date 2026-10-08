using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("字节转十六进制文本")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "字节转十六进制文本；使用固定文化，输入无效时返回节点错误。")]
public sealed class BitConverterToStringNode : BytesNode
{
    public BitConverterToStringNode() : base("字节转十六进制文本", typeof(string))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return BitConverter.ToString(SystemValues.Bytes(values[0]));
    }
}
