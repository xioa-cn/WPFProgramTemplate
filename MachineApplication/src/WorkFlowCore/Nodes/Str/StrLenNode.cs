using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "返回 UTF-16 代码单元数量；部分 emoji 的长度为 2，空字符串为 0。")]
[DisplayName("字符串长度")]
public sealed class StrLenNode : StringNode
{
    public StrLenNode() : base("字符串长度", typeof(int), "返回 UTF-16 代码单元数量；部分 emoji 的长度为 2，空字符串为 0。")
    {
        TextInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    public XTNodeOption TextInput { get; }

    protected override object Calculate(object[] values) => ((string)values[0]).Length;
}
