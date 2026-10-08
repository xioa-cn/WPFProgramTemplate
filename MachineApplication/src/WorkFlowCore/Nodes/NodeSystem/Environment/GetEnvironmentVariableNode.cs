using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("读取环境变量")]
[XTNode("系统环境", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "读取环境变量；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class GetEnvironmentVariableNode : TextValueNode
{
    public GetEnvironmentVariableNode() : base("读取环境变量", typeof(string))
    {
        Text = "PATH";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Environment.GetEnvironmentVariable(SystemValues.Text(values[0]));
    }
}
