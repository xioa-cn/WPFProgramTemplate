using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按顺序连接两个字符串，输出 String。")]
[DisplayName("连接字符串")]
public sealed class StrConcatNode : StringNode
{
    public StrConcatNode() : base("连接字符串", typeof(string), "按顺序连接两个字符串，输出 String。")
    {
        LeftInput = AddInput("左值", () => LeftValue);
        RightInput = AddInput("右值", () => RightValue);
    }

    [XTNodeProperty("左值", "左值输入未连接且未赋值时使用。")]
    public string LeftValue { get; set; } = string.Empty;

    [XTNodeProperty("右值", "右值输入未连接且未赋值时使用。")]
    public string RightValue { get; set; } = string.Empty;

    public XTNodeOption LeftInput { get; }
    public XTNodeOption RightInput { get; }

    protected override object Calculate(object[] values) => (string)values[0] + (string)values[1];
}
