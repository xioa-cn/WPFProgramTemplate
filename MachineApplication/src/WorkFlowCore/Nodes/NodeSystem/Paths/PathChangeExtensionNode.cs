using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("更改扩展名")]
[XTNode("路径操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "更改扩展名；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class PathChangeExtensionNode : TextValueNode
{
    public PathChangeExtensionNode() : base("更改扩展名", typeof(string))
    {
        Text = "data.txt";
        ExtensionInput = AddInput("新扩展名", () => Extension);
    }

    [XTNodeProperty("新扩展名", "例如 .json；空字符串删除扩展名。")]
    public string Extension { get; set; } = ".json";
    public XTNodeOption ExtensionInput { get; }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        _ = SystemValues.Text(values[1]);
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Path.ChangeExtension(SystemValues.Text(values[0]), SystemValues.Text(values[1]));
    }
}
