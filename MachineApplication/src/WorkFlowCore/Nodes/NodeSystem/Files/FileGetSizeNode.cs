using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取文件大小")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取文件大小；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileGetSizeNode : FilePathNode
{
    public FileGetSizeNode() : base("获取文件大小", typeof(long))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return new FileInfo(SystemValues.FullPath(values[0])).Length;
    }
}
