using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class FilePathNode : SystemNode
{
    protected FilePathNode(string title, Type outputType) : base(title, outputType)
    {
        PathInput = AddInput("路径", () => FilePath);
    }

    [XTNodeProperty("路径", "文件或目录路径；相对路径以 exe 所在目录为基准。")]
    public string FilePath { get; set; } = "data.txt";

    public XTNodeOption PathInput { get; }

    protected override void ValidateValues(object?[] values) => _ = SystemValues.FullPath(values[0]);
}
