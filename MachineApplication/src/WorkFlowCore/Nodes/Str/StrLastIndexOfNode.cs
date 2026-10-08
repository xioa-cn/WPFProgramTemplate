using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "返回最后匹配的 UTF-16 索引，从 0 开始；未找到返回 -1，空查找内容返回文本长度。")]
[DisplayName("查找最后位置")]
public sealed class StrLastIndexOfNode : StringNode
{
    public StrLastIndexOfNode() : base("查找最后位置", typeof(int), "返回最后匹配的 UTF-16 索引，从 0 开始；未找到返回 -1，空查找内容返回文本长度。")
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
        ((string)values[0]).LastIndexOf((string)values[1], Comparison(IgnoreCase));
}
