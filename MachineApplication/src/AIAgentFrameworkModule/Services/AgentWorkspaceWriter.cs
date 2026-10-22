using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIAgentFrameworkModule.Services;

public sealed record AgentFileChange(string RelativePath, string Before, string After, string? OriginalHash)
{
    public string Action => OriginalHash is null ? "新建" : "覆盖（自动备份）";
    public string Label => Action + " · " + RelativePath;
}

public sealed class AgentFilePlan
{
    public string Root { get; }
    public IReadOnlyList<AgentFileChange> Files { get; }
    internal bool Consumed { get; set; }
    internal AgentFilePlan(string root, List<AgentFileChange> files) { Root = root; Files = files.AsReadOnly(); }
}

public sealed record AgentFileWriteResult(bool Succeeded, IReadOnlyList<string> Written, string Message);

public sealed class AgentWorkspaceWriter
{
    private const int MaximumBytes = 256 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".xaml", ".csproj", ".sln", ".slnx", ".json", ".md", ".txt", ".xml", ".resx", ".html", ".css", ".js", ".ts", ".tsx", ".jsx", ".vue", ".py", ".sql", ".razor", ".yml", ".yaml" };

    public static string ValidateRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidDataException("请选择本机已存在的专用项目目录，不支持网络路径。");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(full) || full.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("工作目录必须已存在，不能授权整个磁盘。");
        CheckLinks(full);
        return full;
    }

    public AgentFilePlan Prepare(string root, string manifest)
    {
        root = ValidateRoot(root);
        if (manifest.Length > 1_500_000) throw new InvalidDataException("文件清单过大。");
        string json = manifest.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase) && json.EndsWith("```", StringComparison.Ordinal)) json = json[7..^3].Trim();
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        if (!document.RootElement.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array || files.GetArrayLength() is < 1 or > 20)
            throw new InvalidDataException("需要包含 1–20 个文件的 files 清单。");
        List<AgentFileChange> changes = [];
        HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        foreach (JsonElement file in files.EnumerateArray())
        {
            string relative = file.GetProperty("path").GetString() ?? "";
            string content = file.GetProperty("content").GetString() ?? throw new InvalidDataException("文件内容不能为空值。");
            string target = Resolve(root, relative);
            if (!targets.Add(target)) throw new InvalidDataException("清单包含重复文件路径。");
            int bytes = Utf8.GetByteCount(content);
            total += bytes;
            if (content.Contains('\0') || bytes > MaximumBytes || total > 1024 * 1024)
                throw new InvalidDataException("仅支持文本文件：单文件最多 256 KB，总计最多 1 MB。");
            byte[]? original = ReadOriginal(target);
            string before = original is null ? "" : Utf8.GetString(original).TrimStart('\uFEFF');
            if (before.Contains('\0')) throw new InvalidDataException("不能覆盖二进制文件。");
            changes.Add(new AgentFileChange(Path.GetRelativePath(root, target), before, content,
                original is null ? null : Convert.ToHexString(SHA256.HashData(original))));
        }
        return new AgentFilePlan(root, changes);
    }

    public AgentFileWriteResult Apply(AgentFilePlan plan, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        lock (plan)
        {
            if (plan.Consumed) throw new InvalidOperationException("此预览已经使用，请重新生成并确认。");
            plan.Consumed = true;
        }
        List<string> written = [];
        try
        {
            ValidateRoot(plan.Root);
            foreach (AgentFileChange change in plan.Files) VerifyUnchanged(plan.Root, change);
            foreach (AgentFileChange change in plan.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string target = VerifyUnchanged(plan.Root, change);
                string parent = Path.GetDirectoryName(target)!;
                Directory.CreateDirectory(parent);
                CheckLinks(parent);
                string temporary = Path.Combine(parent, ".agent-write-" + Guid.NewGuid().ToString("N") + ".tmp");
                string? backup = null;
                try
                {
                    using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = Utf8.GetBytes(change.After);
                        stream.Write(bytes);
                        stream.Flush(true);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    VerifyUnchanged(plan.Root, change);
                    if (change.OriginalHash is not null)
                    {
                        backup = Path.Combine(parent, ".agent-backup-" + Guid.NewGuid().ToString("N") + ".bak");
                        File.Replace(temporary, target, backup);
                    }
                    else File.Move(temporary, target, false);
                    written.Add(change.RelativePath);
                    progress?.Report($"已写入：{change.RelativePath}" + (backup is null ? "" : $"；备份：{Path.GetRelativePath(plan.Root, backup)}"));
                }
                finally
                {
                    CheckLinks(parent);
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            return new AgentFileWriteResult(true, written.AsReadOnly(), $"实际写入 {written.Count} 个文件；未执行编译或命令。");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            string reason = exception is OperationCanceledException ? "已取消" : exception is InvalidDataException ? exception.Message : "文件被占用、权限不足或目录发生变化";
            string message = $"写入停止：{reason}。已写入 {written.Count} 个文件，不自动回滚；请检查操作记录和备份。";
            progress?.Report(message);
            return new AgentFileWriteResult(false, written.AsReadOnly(), message);
        }
    }

    private static string VerifyUnchanged(string root, AgentFileChange change)
    {
        string target = Resolve(root, change.RelativePath);
        byte[]? bytes = ReadOriginal(target);
        string? hash = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
        if (hash != change.OriginalHash) throw new InvalidDataException("预览后文件已改变，拒绝覆盖：" + change.RelativePath);
        return target;
    }

    private static byte[]? ReadOriginal(string target)
    {
        if (!File.Exists(target)) return null;
        using FileStream stream = new(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("已有文件超过 256 KB，不能覆盖。");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new InvalidDataException("模型必须使用工作目录内的相对路径。");
        string[] segments = relative.Replace('/', '\\').Split('\\');
        foreach (string segment in segments)
        {
            string stem = segment.Split('.')[0].ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(segment) || segment.StartsWith('.') || segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])) ||
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase) || segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("不允许隐藏目录、保留名称、输出目录或路径穿越。");
        }
        if (!Extensions.Contains(Path.GetExtension(relative))) throw new InvalidDataException("不支持的文件类型，仅允许源代码和文本配置。");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("目标超出授权目录。");
        CheckLinks(full);
        if (Directory.Exists(full)) throw new InvalidDataException("目标是目录，不能覆盖。");
        return full;
    }

    private static void CheckLinks(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("不允许符号链接、目录联接或其他重解析点。");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
