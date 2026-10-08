using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("创建目录")]
[XTNode("目录操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "创建目录；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class DirectoryCreateNode : FilePathNode
{
    public DirectoryCreateNode() : base("创建目录", typeof(string))
    {
        FilePath = "data";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Directory.CreateDirectory(SystemValues.FullPath(values[0])).FullName;
    }
}
