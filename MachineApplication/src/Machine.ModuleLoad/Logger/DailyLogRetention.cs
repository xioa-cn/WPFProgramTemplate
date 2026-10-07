using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Machine.ModuleLoad.Logger;

/// <summary>为两套日志实现统一的按日期目录保留策略，在后台启动检查并每小时重试。</summary>
internal sealed class DailyLogRetention : IDisposable
{
    private readonly string _logDirectory;
    private readonly int _retentionDays;
    private readonly Timer? _timer;
    private int _running;
    private int _disposed;

    public DailyLogRetention(LoggingOptions options)
    {
        _logDirectory = Path.TrimEndingDirectorySeparator(options.ResolveLogDirectory());
        _retentionDays = options.RetentionDays;
        if (_retentionDays > 0)
            _timer = new Timer(Cleanup, null, TimeSpan.Zero, TimeSpan.FromHours(1));
    }

    /// <summary>跳过重叠回调，以当地自然日计算过期边界，不依赖最后一次写日志的日期。</summary>
    private void Cleanup(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        try
        {
            var root = new DirectoryInfo(_logDirectory);
            if (!IsSafeDirectory(root)) return;
            var today = DateOnly.FromDateTime(DateTime.Now);
            var firstRetainedDay = Math.Max(0L, (long)today.DayNumber - _retentionDays + 1);
            foreach (var directory in root.EnumerateDirectories())
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                if (!DateOnly.TryParseExact(directory.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date) || date.DayNumber >= firstRetainedDay)
                    continue;

                try { CleanupDirectory(directory); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    ReportFailure(directory.FullName, exception);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportFailure(_logDirectory, exception);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>仅删除直接日期子目录中的 log.log 和 log_数字.log，不递归删除目录或用户文件。</summary>
    private void CleanupDirectory(DirectoryInfo directory)
    {
        if (!string.Equals(directory.Parent?.FullName, _logDirectory, StringComparison.OrdinalIgnoreCase) ||
            !IsSafeDirectory(directory)) return;

        foreach (var file in directory.EnumerateFiles())
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            if (!IsLogFile(file.Name)) continue;
            try
            {
                // 每次删除前重新校验目录和文件，避免沿符号链接或目录联接访问日志根目录以外的数据。
                if (!IsSafeDirectory(directory)) return;
                file.Refresh();
                if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                file.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 文件占用或权限不足不影响正常日志写入，下次定时检查继续重试。
                ReportFailure(file.FullName, exception);
            }
        }

        if (Volatile.Read(ref _disposed) == 0 && IsSafeDirectory(directory) &&
            !directory.EnumerateFileSystemInfos().Any())
            directory.Delete(false);
    }

    /// <summary>拒绝目录及其任意父级的重解析点；不存在的日志目录无需创建或清理。</summary>
    private static bool IsSafeDirectory(DirectoryInfo directory)
    {
        for (DirectoryInfo? current = directory; current is not null; current = current.Parent)
        {
            current.Refresh();
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
        }
        return true;
    }

    /// <summary>匹配当前 Serilog 与 NLog 配置生成的固定文件名及数字滚动后缀。</summary>
    private static bool IsLogFile(string name)
    {
        if (name.Equals("log.log", StringComparison.OrdinalIgnoreCase)) return true;
        if (!name.StartsWith("log_", StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || name.Length <= 8) return false;
        return name.AsSpan(4, name.Length - 8).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>使用诊断输出，避免清理错误再次写入正在清理的日志库造成递归。</summary>
    private static void ReportFailure(string path, Exception exception)
        => Debug.WriteLine($"日志保留清理失败，将在下次检查重试：{path}，{exception.Message}");

    /// <summary>停止后续调度；已启动的清理会在下个文件检查点退出，不阻塞 UI 等待磁盘扫描。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _timer?.Dispose();
    }
}
