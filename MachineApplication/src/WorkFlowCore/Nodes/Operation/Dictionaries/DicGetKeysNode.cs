using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("获取字典键")]
[XTNode("字典操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取字典键；键区分大小写。修改输出副本，缺失键读取报错、删除忽略。")]
public sealed class DicGetKeysNode : DictionaryOperationNode
{
    public DicGetKeysNode() : base("获取字典键", typeof(string[]))
    {

    }


    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var dictionary = OperationValues.Dictionary(values[0]);
        return dictionary.Keys.ToArray();
    }
}
