using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

[XTNode("字符串", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "使用分隔符连接 String[]，可直接连接拆分节点；默认数组使用 JSON 文本配置。")]
[DisplayName("连接数组")]
public sealed class StrJoinNode : StringNode
{
    public StrJoinNode() : base("连接数组", typeof(string), "使用分隔符连接 String[]，可直接连接拆分节点；默认数组使用 JSON 文本配置。")
    {
        ValuesInput = AddInput("字符串数组", ReadDefaultValues);
        SeparatorInput = AddInput("分隔符", () => Separator);
    }

    [XTNodeProperty("默认数组（JSON）", "数组输入未连接且未赋值时使用，例如 [\"甲\",\"乙\"]；[] 表示空数组。")]
    public string ValuesJson { get; set; } = "[]";

    [XTNodeProperty("分隔符", "分隔符输入未连接且未赋值时使用；为空表示直接拼接。")]
    public string Separator { get; set; } = ",";

    public XTNodeOption ValuesInput { get; }
    public XTNodeOption SeparatorInput { get; }

    private string[] ReadDefaultValues() =>
        System.Text.Json.JsonSerializer.Deserialize<string[]>(ValuesJson)
        ?? throw new ArgumentException("默认数组必须为 JSON 字符串数组，不能为 null。");

    protected override object Calculate(object[] values) => string.Join((string)values[1], (string[])values[0]);
}
