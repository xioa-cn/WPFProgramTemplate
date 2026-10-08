using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按 UTF-16 索引截取文本；索引从 0 开始，长度为 -1 表示截取到末尾，越界时报错。")]
[DisplayName("截取")]
public sealed class StrSubstringNode : StringNode
{
    public StrSubstringNode() : base("截取", typeof(string), "按 UTF-16 索引截取文本；索引从 0 开始，长度为 -1 表示截取到末尾，越界时报错。")
    {
        TextInput = AddInput("文本", () => Text);
        StartInput = AddInput("起始位置", () => StartIndex);
        LengthInput = AddInput("长度", () => Length);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("起始位置", "从 0 开始，允许等于字符串长度。")]
    public int StartIndex { get; set; }

    [XTNodeProperty("长度", "-1 表示到末尾，0 表示空字符串，其余值必须非负且不能越界。")]
    public int Length { get; set; } = -1;

    public XTNodeOption TextInput { get; }
    public XTNodeOption StartInput { get; }
    public XTNodeOption LengthInput { get; }

    protected override void ValidateValues(object[] values)
    {
        var text = (string)values[0];
        var start = (int)values[1];
        var length = (int)values[2];
        if (start < 0 || start > text.Length) throw new ArgumentOutOfRangeException(nameof(StartIndex), "起始位置超出字符串范围。");
        if (length < -1 || length > text.Length - start) throw new ArgumentOutOfRangeException(nameof(Length), "截取长度超出允许范围。");
    }

    protected override object Calculate(object[] values) => (int)values[2] == -1
        ? ((string)values[0]).Substring((int)values[1])
        : ((string)values[0]).Substring((int)values[1], (int)values[2]);
}
