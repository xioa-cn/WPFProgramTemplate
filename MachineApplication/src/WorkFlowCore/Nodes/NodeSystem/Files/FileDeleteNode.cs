using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("删除文件")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "删除文件；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileDeleteNode : FilePathNode
{
    public FileDeleteNode() : base("删除文件", typeof(string))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var path = SystemValues.FullPath(values[0]);
        File.Delete(path);
        return path;
    }
}
