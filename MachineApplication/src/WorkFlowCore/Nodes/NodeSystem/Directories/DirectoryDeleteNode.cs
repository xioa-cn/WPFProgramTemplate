using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("删除目录")]
[XTNode("目录操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "删除目录；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class DirectoryDeleteNode : FilePathNode
{
    public DirectoryDeleteNode() : base("删除目录", typeof(string))
    {
        FilePath = "data";
    }

    [XTNodeProperty("递归删除", "默认仅删除空目录；启用会删除目标目录及其全部内容，请谨慎使用。")]
    public bool Recursive { get; set; }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        SystemValues.CheckDeletePath(SystemValues.FullPath(values[0]));
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var path = SystemValues.FullPath(values[0]);
        Directory.Delete(path, Recursive);
        return path;
    }
}
