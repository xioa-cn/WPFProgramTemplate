using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取程序目录")]
[XTNode("系统环境", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取程序目录；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class GetExecutableDirectoryNode : SystemNode
{
    public GetExecutableDirectoryNode() : base("获取程序目录", typeof(string))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return AppContext.BaseDirectory;
    }
}
