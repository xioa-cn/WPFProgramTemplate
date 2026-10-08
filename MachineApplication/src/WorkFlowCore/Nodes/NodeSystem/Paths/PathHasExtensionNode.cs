using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("路径是否有扩展名")]
[XTNode("路径操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "路径是否有扩展名；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class PathHasExtensionNode : TextValueNode
{
    public PathHasExtensionNode() : base("路径是否有扩展名", typeof(bool))
    {
        Text = "data.txt";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Path.HasExtension(SystemValues.Text(values[0]));
    }
}
