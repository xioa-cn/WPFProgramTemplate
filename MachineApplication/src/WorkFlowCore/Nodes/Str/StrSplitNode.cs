using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按完整分隔字符串拆分，输出 String[]；分隔符为空时不拆分。")]
[DisplayName("拆分")]
public sealed class StrSplitNode : StringNode
{
    public StrSplitNode() : base("拆分", typeof(string[]), "按完整分隔字符串拆分，输出 String[]；分隔符为空时不拆分。")
    {
        TextInput = AddInput("文本", () => Text);
        SeparatorInput = AddInput("分隔符", () => Separator);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("分隔符", "使用完整字符串匹配，例如 ::；不使用正则表达式。")]
    public string Separator { get; set; } = ",";

    [XTNodeProperty("移除空项", "移除结果中的空字符串；与去除空白同时启用时，纯空白项也会移除。")]
    public bool RemoveEmptyEntries { get; set; }

    [XTNodeProperty("去除项首尾空白", "去除每项的首尾空白。")]
    public bool TrimEntries { get; set; }

    public XTNodeOption TextInput { get; }
    public XTNodeOption SeparatorInput { get; }

    protected override object Calculate(object[] values)
    {
        var options = StringSplitOptions.None;
        if (RemoveEmptyEntries) options |= StringSplitOptions.RemoveEmptyEntries;
        if (TrimEntries) options |= StringSplitOptions.TrimEntries;
        return ((string)values[0]).Split((string)values[1], options);
    }
}
