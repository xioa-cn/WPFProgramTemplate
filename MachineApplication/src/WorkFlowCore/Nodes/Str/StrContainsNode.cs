using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "判断是否包含指定内容；空查找内容返回 true。")]
[DisplayName("包含")]
public sealed class StrContainsNode : StringNode
{
    public StrContainsNode() : base("包含", typeof(bool), "判断是否包含指定内容；空查找内容返回 true。")
    {
        TextInput = AddInput("文本", () => Text);
        SearchInput = AddInput("查找内容", () => Search);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("查找内容", "输入未连接且未赋值时使用。")]
    public string Search { get; set; } = string.Empty;

    [XTNodeProperty("忽略大小写", "使用序号比较，不受系统区域设置影响。")]
    public bool IgnoreCase { get; set; }

    public XTNodeOption TextInput { get; }
    public XTNodeOption SearchInput { get; }

    protected override object Calculate(object[] values) =>
        ((string)values[0]).Contains((string)values[1], Comparison(IgnoreCase));
}
