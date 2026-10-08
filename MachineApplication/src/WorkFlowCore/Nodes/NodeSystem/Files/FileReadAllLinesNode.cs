using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("读取文件行")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "读取文件行；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileReadAllLinesNode : TextFileNode
{
    public FileReadAllLinesNode() : base("读取文件行", typeof(string[]))
    {

    }


    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken)
    {
        return await File.ReadAllLinesAsync(SystemValues.FullPath(values[0]), SystemValues.Encoding(EncodingKind), cancellationToken).ConfigureAwait(false);
    }
}
