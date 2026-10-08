using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("列出目录文件")]
[XTNode("目录操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "列出目录文件；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class DirectoryGetFilesNode : DirectoryEnumerationNode
{
    public DirectoryGetFilesNode() : base("列出目录文件")
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Enumerate(SystemValues.FullPath(values[0]), false, cancellationToken);
    }
}
