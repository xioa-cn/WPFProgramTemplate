using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("退出程序")]
[XTNode("系统环境", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "退出程序；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class EnvironmentExitNode : SystemNode
{
    public EnvironmentExitNode() : base("退出程序", typeof(int))
    {

    }

    [XTNodeProperty("允许退出", "危险操作，默认关闭；启用后执行将立即结束整个进程，未保存数据可能丢失。")]
    public bool AllowExit { get; set; }
    [XTNodeProperty("退出代码", "进程退出代码，0 通常表示正常结束。")]
    public int ExitCode { get; set; }
    protected override void ValidateSettings()
    {
        if (!AllowExit) throw new InvalidOperationException("退出程序节点未启用“允许退出”。");
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Environment.Exit(ExitCode);
        return ExitCode;
    }
}
