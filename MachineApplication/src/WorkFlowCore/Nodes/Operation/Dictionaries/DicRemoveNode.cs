using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("删除字典键")]
[XTNode("字典操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "删除字典键；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicRemoveNode : DictionaryOperationNode
{
    public DicRemoveNode() : base("删除字典键", typeof(object))
    {
        KeyInput = AddInput("键", () => Key);
    }

    [XTNodeProperty("键", "字符串键区分大小写，允许空字符串，不允许 null。")]
    public string Key { get; set; } = "key";

    public XTNodeOption KeyInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        dictionary.Remove(OperationValues.Key(values[1]));
        return dictionary;
    }
}
