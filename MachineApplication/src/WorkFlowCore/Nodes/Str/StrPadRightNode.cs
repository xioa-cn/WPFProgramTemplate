using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "填充到指定总长度；长度不超过文本长度时保持原样，不截断文本。")]
[DisplayName("右侧填充")]
public sealed class StrPadRightNode : StringNode
{
    public StrPadRightNode() : base("右侧填充", typeof(string), "填充到指定总长度；长度不超过文本长度时保持原样，不截断文本。")
    {
        TextInput = AddInput("文本", () => Text);
        WidthInput = AddInput("总长度", () => TotalWidth);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("总长度", "目标字符串总长度，必须为非负 Int32。")]
    public int TotalWidth { get; set; }

    [XTNodeProperty("填充字符", "必须恰好为一个 UTF-16 字符，默认空格。")]
    public string PaddingCharacter { get; set; } = " ";

    public XTNodeOption TextInput { get; }
    public XTNodeOption WidthInput { get; }

    protected override void ValidateSettings()
    {
        if (PaddingCharacter is null || PaddingCharacter.Length != 1)
            throw new ArgumentException("填充字符必须恰好为一个 UTF-16 字符。", nameof(PaddingCharacter));
    }

    protected override void ValidateValues(object[] values)
    {
        if ((int)values[1] < 0) throw new ArgumentOutOfRangeException(nameof(TotalWidth), "总长度不能小于 0。");
    }

    protected override object Calculate(object[] values) =>
        ((string)values[0]).PadRight((int)values[1], PaddingCharacter[0]);
}
