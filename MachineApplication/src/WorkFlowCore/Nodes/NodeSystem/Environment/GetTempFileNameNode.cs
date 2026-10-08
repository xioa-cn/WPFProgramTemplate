using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("创建临时文件")]
[XTNode("系统环境", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "创建临时文件；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class GetTempFileNameNode : SystemNode
{
    public GetTempFileNameNode() : base("创建临时文件", typeof(string))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Path.GetTempFileName();
    }
}
