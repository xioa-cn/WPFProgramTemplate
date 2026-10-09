using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("字典包含值")]
[XTNode("字典操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "字典包含值；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicContainsValueNode : DictionaryOperationNode
{
    public DicContainsValueNode() : base("字典包含值", typeof(bool))
    {
        ValueInput = AddInput("值", () => OperationValues.Parse(ValueJson));
    }

    [XTNodeProperty("值", "例如 123、true、null 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        return dictionary.Values.Any(item => OperationValues.Equal(item, values[1]));
    }
}
