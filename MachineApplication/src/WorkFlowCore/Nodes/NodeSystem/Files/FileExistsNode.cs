using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("文件是否存在")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "文件是否存在；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileExistsNode : FilePathNode
{
    public FileExistsNode() : base("文件是否存在", typeof(bool))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return File.Exists(SystemValues.FullPath(values[0]));
    }
}
