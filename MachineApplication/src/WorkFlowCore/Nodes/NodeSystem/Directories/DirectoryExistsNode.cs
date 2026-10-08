using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("目录是否存在")]
[XTNode("目录操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "目录是否存在；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class DirectoryExistsNode : FilePathNode
{
    public DirectoryExistsNode() : base("目录是否存在", typeof(bool))
    {
        FilePath = "data";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Directory.Exists(SystemValues.FullPath(values[0]));
    }
}
