using System.IO;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class DirectoryEnumerationNode : FilePathNode
{
    protected DirectoryEnumerationNode(string title) : base(title, typeof(string[])) { FilePath = "."; }

    [XTNodeProperty("搜索模式", "仅文件或目录名称的通配符，例如 *.txt，不支持包含路径分隔符。")]
    public string SearchPattern { get; set; } = "*";

    [XTNodeProperty("包含子目录", "递归遍历；跳过重解析点，避免跟随目录链接。")]
    public bool Recursive { get; set; }

    [XTNodeProperty("最大结果数", "超过限制报错，避免占用过多内存。")]
    public int MaxResults { get; set; } = 10000;

    protected override void ValidateSettings()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SearchPattern);
        if (SearchPattern.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("搜索模式不能包含路径。");
        if (MaxResults < 1) throw new ArgumentOutOfRangeException(nameof(MaxResults));
    }

    protected string[] Enumerate(string path, bool directories, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = Recursive,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = MatchType.Simple
        };
        var entries = directories ? Directory.EnumerateDirectories(path, SearchPattern, options) : Directory.EnumerateFiles(path, SearchPattern, options);
        var results = new List<string>();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count >= MaxResults) throw new IOException("目录结果超过最大数量限制。");
            results.Add(entry);
        }
        return results.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
