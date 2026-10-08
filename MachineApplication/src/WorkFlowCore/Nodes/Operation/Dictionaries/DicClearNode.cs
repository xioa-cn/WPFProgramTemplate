using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("清空字典")]
[XTNode("字典操作", "xioa", "", "", "清空字典；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicClearNode : DictionaryOperationNode
{
    public DicClearNode() : base("清空字典", typeof(object))
    {

    }


    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
