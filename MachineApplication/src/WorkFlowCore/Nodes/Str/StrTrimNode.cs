using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "移除文本两端的空白或指定字符集合，不修改文本中间的内容。")]
[DisplayName("去除首尾字符")]
public sealed class StrTrimNode : StringNode
{
    public StrTrimNode() : base("去除首尾字符", typeof(string), "移除文本两端的空白或指定字符集合，不修改文本中间的内容。")
    {
        TextInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("要移除的字符", "为空时移除 Unicode 空白；否则按字符集合移除，不按完整子串匹配。")]
    public string Characters { get; set; } = string.Empty;

    public XTNodeOption TextInput { get; }

    protected override object Calculate(object[] values) => string.IsNullOrEmpty(Characters)
        ? ((string)values[0]).Trim()
        : ((string)values[0]).Trim(Characters.ToCharArray());
}
