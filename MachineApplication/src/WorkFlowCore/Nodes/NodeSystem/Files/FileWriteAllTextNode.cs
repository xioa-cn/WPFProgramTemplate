using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("写入文件文本")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "写入文件文本；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileWriteAllTextNode : TextFileNode
{
    public FileWriteAllTextNode() : base("写入文件文本", typeof(string))
    {
        TextInput = AddInput("文本", () => Text);
    }

    [XTNodeProperty("文本", "写入的原始文本；写入节点覆盖原文件，追加节点保留原内容。")]
    public string Text { get; set; } = string.Empty;

    public new XTNodeOption TextInput { get; }

    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        _ = SystemValues.Text(values[1]);
    }
    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken)
    {
        var path = SystemValues.FullPath(values[0]);
        await File.WriteAllTextAsync(path, SystemValues.Text(values[1]), SystemValues.Encoding(EncodingKind), cancellationToken).ConfigureAwait(false);
        return path;
    }
}
