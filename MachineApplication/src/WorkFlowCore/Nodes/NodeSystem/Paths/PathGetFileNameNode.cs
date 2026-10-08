using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取文件名")]
[XTNode("路径操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取文件名；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class PathGetFileNameNode : TextValueNode
{
    public PathGetFileNameNode() : base("获取文件名", typeof(string))
    {
        Text = "data.txt";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Path.GetFileName(SystemValues.Text(values[0]));
    }
}
