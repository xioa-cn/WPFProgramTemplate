using System.ComponentModel;
using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("获取文件时间")]
[XTNode("文件操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取文件时间；仅执行时访问系统资源，失败不输出完成信号。")]
public sealed class FileGetTimeNode : FilePathNode
{
    public FileGetTimeNode() : base("获取文件时间", typeof(DateTime))
    {

    }

    [XTNodeProperty("时间类型", "Creation 创建时间、LastWrite 修改时间、LastAccess 访问时间。")]
    public FileTimestampKind TimestampKind { get; set; } = FileTimestampKind.LastWrite;
    [XTNodeProperty("UTC 时间", "启用返回 UTC，关闭返回本地时间。")]
    public bool UseUtc { get; set; }
    protected override void ValidateSettings()
    {
        if (!Enum.IsDefined(TimestampKind)) throw new ArgumentOutOfRangeException(nameof(TimestampKind));
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var info = new FileInfo(SystemValues.FullPath(values[0]));
        if (!info.Exists) throw new FileNotFoundException("文件不存在。", info.FullName);
        return TimestampKind switch
        {
            FileTimestampKind.Creation => UseUtc ? info.CreationTimeUtc : info.CreationTime,
            FileTimestampKind.LastWrite => UseUtc ? info.LastWriteTimeUtc : info.LastWriteTime,
            _ => UseUtc ? info.LastAccessTimeUtc : info.LastAccessTime
        };
    }
}
