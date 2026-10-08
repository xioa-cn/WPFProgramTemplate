using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "替换所有匹配内容；旧内容不能为空，新内容为空表示删除匹配内容。")]
[DisplayName("替换")]
public sealed class StrReplaceNode : StringNode
{
    public StrReplaceNode() : base("替换", typeof(string), "替换所有匹配内容；旧内容不能为空，新内容为空表示删除匹配内容。")
    {
        TextInput = AddInput("文本", () => Text);
        OldInput = AddInput("旧内容", () => OldValue);
        NewInput = AddInput("新内容", () => NewValue);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("旧内容", "被替换的内容，不能为空。")]
    public string OldValue { get; set; } = string.Empty;

    [XTNodeProperty("新内容", "替换后的内容；空字符串表示删除。")]
    public string NewValue { get; set; } = string.Empty;

    [XTNodeProperty("忽略大小写", "使用序号比较，不受系统区域设置影响。")]
    public bool IgnoreCase { get; set; }

    public XTNodeOption TextInput { get; }
    public XTNodeOption OldInput { get; }
    public XTNodeOption NewInput { get; }

    protected override void ValidateValues(object[] values)
    {
        if (((string)values[1]).Length == 0) throw new ArgumentException("旧内容不能为空。");
    }

    protected override object Calculate(object[] values) =>
        ((string)values[0]).Replace((string)values[1], (string)values[2], Comparison(IgnoreCase));
}
