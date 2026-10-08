using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("获取字典值")]
[XTNode("字典操作", "xioa", "", "", "获取字典值；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicGetValuesNode : DictionaryOperationNode
{
    public DicGetValuesNode() : base("获取字典值", typeof(object[]))
    {

    }


    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        return dictionary.Values.ToArray();
    }
}
