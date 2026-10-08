using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public abstract class DictionaryOperationNode : OperationNode
{
    protected DictionaryOperationNode(string title, Type outputType) : base(title, outputType)
    {
        DictionaryInput = AddInput("字典", () => OperationValues.Parse(EntriesJson));
    }

    [XTNodeProperty("字典（JSON）", "未连接时使用的 JSON 对象；字符串键区分大小写，修改操作输出副本。")]
    public string EntriesJson { get; set; } = "{\"key\":1}";

    public XTNodeOption DictionaryInput { get; }
}
