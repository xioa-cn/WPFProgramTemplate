using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("设置字典值")]
[XTNode("字典操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "设置字典值；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicSetNode : DictionaryOperationNode
{
    public DicSetNode() : base("设置字典值", typeof(object))
    {
        KeyInput = AddInput("键", () => Key);
        ValueInput = AddInput("值", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("键", "字符串键区分大小写，允许空字符串，不允许 null。")]
    public string Key { get; set; } = "key";

    public XTNodeOption KeyInput { get; }

    [XTNodeProperty("值", "例如 123、true、null 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        dictionary[OperationValues.Key(values[1])] = values[2];
        return dictionary;
    }
}
