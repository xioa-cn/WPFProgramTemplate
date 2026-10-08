using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按序号比较两个字符串，左值小于、等于、大于右值时分别输出 -1、0、1。")]
[DisplayName("比较字符串")]
public sealed class StrCompareNode : StringNode
{
    public StrCompareNode() : base("比较字符串", typeof(int), "按序号比较两个字符串，左值小于、等于、大于右值时分别输出 -1、0、1。")
    {
        LeftInput = AddInput("左值", () => LeftValue);
        RightInput = AddInput("右值", () => RightValue);
    }

    [XTNodeProperty("左值", "左值输入未连接且未赋值时使用。")]
    public string LeftValue { get; set; } = string.Empty;

    [XTNodeProperty("右值", "右值输入未连接且未赋值时使用。")]
    public string RightValue { get; set; } = string.Empty;

    [XTNodeProperty("忽略大小写", "使用序号比较，不受系统区域设置影响。")]
    public bool IgnoreCase { get; set; }

    public XTNodeOption LeftInput { get; }
    public XTNodeOption RightInput { get; }

    protected override object Calculate(object[] values) =>
        global::System.Math.Sign(string.Compare((string)values[0], (string)values[1], Comparison(IgnoreCase)));
}
