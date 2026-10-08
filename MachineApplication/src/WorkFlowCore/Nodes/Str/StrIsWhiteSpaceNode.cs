using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "判断文本是否为空或只包含空格、制表符、换行等空白字符。")]
[DisplayName("是否为空白")]
public sealed class StrIsWhiteSpaceNode : StringNode
{
    public StrIsWhiteSpaceNode() : base("是否为空白", typeof(bool), "判断文本是否为空或只包含空格、制表符、换行等空白字符。")
    {
        TextInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    public XTNodeOption TextInput { get; }

    protected override object Calculate(object[] values) => string.IsNullOrWhiteSpace((string)values[0]);
}
