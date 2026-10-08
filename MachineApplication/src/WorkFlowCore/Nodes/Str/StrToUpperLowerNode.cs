using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "使用固定文化转换大小写，不受系统语言影响。")]
[DisplayName("大小写转换")]
public sealed class StrToUpperLowerNode : StringNode
{
    public StrToUpperLowerNode() : base("大小写转换", typeof(string), "使用固定文化转换大小写，不受系统语言影响。")
    {
        TextInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "文本输入未连接且未赋值时使用；空值按空字符串处理。")]
    public string Text { get; set; } = string.Empty;

    [XTNodeProperty("转换方式", "Upper：大写；Lower：小写。")]
    public StringCaseMode Mode { get; set; } = StringCaseMode.Upper;

    public XTNodeOption TextInput { get; }

    protected override void ValidateSettings()
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode), "不支持的大小写转换方式。");
    }

    protected override object Calculate(object[] values) => Mode == StringCaseMode.Upper
        ? ((string)values[0]).ToUpperInvariant()
        : ((string)values[0]).ToLowerInvariant();
}
