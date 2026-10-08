using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class TextFileNode : FilePathNode
{
    protected TextFileNode(string title, Type outputType) : base(title, outputType) { }

    [XTNodeProperty("编码", "默认 UTF-8 无 BOM；读取时自动识别 BOM。")]
    public TextEncodingKind EncodingKind { get; set; } = TextEncodingKind.Utf8;

    protected override void ValidateSettings() => _ = SystemValues.Encoding(EncodingKind);
}
