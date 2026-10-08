using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("组合路径")]
[XTNode("路径操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "组合路径；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class PathCombineNode : TextValueNode
{
    public PathCombineNode() : base("组合路径", typeof(string))
    {
        Text = "data";
        SecondInput = AddInput("第二段", () => SecondPath);
    }

    [XTNodeProperty("第二段", "第二段路径；如果为绝对路径，则遵循 Path.Combine 规则覆盖前段。")]
    public string SecondPath { get; set; } = "file.txt";
    public XTNodeOption SecondInput { get; }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        _ = SystemValues.Text(values[1]);
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Path.Combine(SystemValues.Text(values[0]), SystemValues.Text(values[1]));
    }
}
