using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Machine.ModuleLoad.Logger;

/// <summary>将 EF Core 的 SQL 执行事件转发到现有 GlobalLogger。</summary>
/// <remarks>
/// 正常 SQL 使用 Info 级别，取消使用 Warn，失败使用 Error；事件内容包含执行耗时和完整 SQL。
/// 不主动开启敏感数据日志，因此 EF Core 参数默认不会展开为真实值，避免密码等数据进入日志。
/// </remarks>
public static class EFLogger
{
    /// <summary>为 DbContext 选项接入全局 EF Core 日志。</summary>
    /// <remarks>该配置会覆盖同一个 optionsBuilder 上此前注册的 LogTo 回调。</remarks>
    public static DbContextOptionsBuilder UseGlobalLogger(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        return optionsBuilder.LogTo(ShouldLog, Write);
    }

    /// <summary>泛型 DbContextOptionsBuilder 的链式调用重载。</summary>
    public static DbContextOptionsBuilder<TContext> UseGlobalLogger<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        UseGlobalLogger((DbContextOptionsBuilder)optionsBuilder);
        return optionsBuilder;
    }

    /// <summary>只记录命令完成、失败、取消和 Info 以上事件，避免同一条 SQL 重复记录开始与完成事件。</summary>
    private static bool ShouldLog(EventId eventId, LogLevel level)
        => level != LogLevel.None && (level >= LogLevel.Information ||
            eventId == RelationalEventId.CommandExecuted ||
            eventId == RelationalEventId.CommandError ||
            eventId == RelationalEventId.CommandCanceled);

    /// <summary>保留 EF 原始事件信息和 CommandText，并转发到统一日志库。</summary>
    private static void Write(EventData eventData)
    {
        try
        {
            var message = $"[EFCore] [{eventData.EventId.Name}:{eventData.EventId.Id}] {eventData}";
            if (eventData is CommandEventData command)
            {
                message = $"[CommandId={command.CommandId}] {message}";
                var sql = command.Command.CommandText;
                if (!string.IsNullOrWhiteSpace(sql) && !message.Contains(sql, StringComparison.Ordinal))
                    message += Environment.NewLine + sql;
            }

            if (eventData is CommandErrorEventData commandError)
            {
                GlobalLogger.Error(message, commandError.Exception);
                return;
            }

            if (eventData.EventId == RelationalEventId.CommandCanceled)
            {
                GlobalLogger.Warn(message);
                return;
            }

            if (eventData.EventId == RelationalEventId.CommandExecuted)
            {
                GlobalLogger.Info(message);
                return;
            }

            switch (eventData.LogLevel)
            {
                case LogLevel.Critical:
                    GlobalLogger.Fatal(message);
                    break;
                case LogLevel.Error:
                    GlobalLogger.Error(message);
                    break;
                case LogLevel.Warning:
                    GlobalLogger.Warn(message);
                    break;
                case LogLevel.Information:
                    GlobalLogger.Info(message);
                    break;
            }
        }
        catch (Exception exception)
        {
            // 日志转发失败不能影响已经执行的数据库操作，也不能再次调用 GlobalLogger 形成递归。
            Debug.WriteLine($"EF Core 日志转发失败：{exception}");
        }
    }
}
