using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "判断是否以指定前缀开头；空前缀返回 true。")]
[DisplayName("以指定内容开头")]
public sealed class StrStartsWithNode : StringNode
{
    public StrStartsWithNode() : base("以指定内容开头", typeof(bool), "判断是否以指定前缀开头；空前缀返回 true。")
    {
        TextInput = AddInput("文本", () => Text);
        PrefixInput = AddInput("前缀", () => Prefix);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("前缀", "输入未连接且未赋值时使用。")]
    public string Prefix { get; set; } = string.Empty;

    [XTNodeProperty("忽略大小写", "使用序号比较，不受系统区域设置影响。")]
    public bool IgnoreCase { get; set; }

    public XTNodeOption TextInput { get; }
    public XTNodeOption PrefixInput { get; }

    protected override object Calculate(object[] values) =>
        ((string)values[0]).StartsWith((string)values[1], Comparison(IgnoreCase));
}
