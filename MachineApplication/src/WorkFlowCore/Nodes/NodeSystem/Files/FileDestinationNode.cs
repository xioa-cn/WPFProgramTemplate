using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class FileDestinationNode : FilePathNode
{
    protected FileDestinationNode(string title) : base(title, typeof(string))
    {
        DestinationInput = AddInput("目标路径", () => DestinationPath);
    }

    [XTNodeProperty("目标路径", "目标文件的完整路径；相对路径基于 exe 目录，不自动创建父目录。")]
    public string DestinationPath { get; set; } = "data-copy.txt";

    [XTNodeProperty("覆盖目标", "默认不覆盖；启用后可替换已有目标文件。")]
    public bool Overwrite { get; set; }

    public XTNodeOption DestinationInput { get; }

    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        if (string.Equals(SystemValues.FullPath(values[0]), SystemValues.FullPath(values[1]), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("源路径和目标路径不能相同。");
    }
}
