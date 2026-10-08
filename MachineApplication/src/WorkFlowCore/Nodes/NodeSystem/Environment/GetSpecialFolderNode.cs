using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取特殊目录")]
[XTNode("系统环境", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取特殊目录；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class GetSpecialFolderNode : SystemNode
{
    public GetSpecialFolderNode() : base("获取特殊目录", typeof(string))
    {

    }

    [XTNodeProperty("特殊目录", "选择系统已知目录；目录不存在时可能返回空字符串，不创建目录。")]
    public Environment.SpecialFolder Folder { get; set; } = Environment.SpecialFolder.MyDocuments;
    protected override void ValidateSettings()
    {
        if (!Enum.IsDefined(Folder)) throw new ArgumentOutOfRangeException(nameof(Folder));
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return Environment.GetFolderPath(Folder);
    }
}
