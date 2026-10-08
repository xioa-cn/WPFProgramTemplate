using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("移动文件")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "移动文件；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileMoveNode : FileDestinationNode
{
    public FileMoveNode() : base("移动文件")
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var destination = SystemValues.FullPath(values[1]);
        File.Move(SystemValues.FullPath(values[0]), destination, Overwrite);
        return destination;
    }
}
