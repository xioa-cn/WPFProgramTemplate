using System.IO;
using Microsoft.Extensions.Options;

namespace Machine.ModuleLoad.Logger;

public sealed class LoggingOptions
{
    public const string SectionName = "Logging";

    public LoggingProvider Provider { get; set; } = LoggingProvider.Serilog;

    public string LogDirectory { get; set; } = "Logs";

    public LoggingLevel MinimumLevel { get; set; } = LoggingLevel.Trace;

    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;

    public int RetainedFileCountLimit { get; set; } = 31;

    internal string ResolveLogDirectory() => Path.GetFullPath(LogDirectory, AppContext.BaseDirectory);

    internal void Validate()
    {
        var result = new LoggingOptionsValidator().Validate(Options.DefaultName, this);
        if (result.Failed)
            throw new OptionsValidationException(Options.DefaultName, typeof(LoggingOptions), result.Failures);
    }
}

public enum LoggingProvider
{
    Serilog,
    NLog
}

public enum LoggingLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

internal sealed class LoggingOptionsValidator : IValidateOptions<LoggingOptions>
{
    public ValidateOptionsResult Validate(string? name, LoggingOptions options)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(options.Provider)) errors.Add("Logging:Provider 必须为 Serilog 或 NLog。");
        if (!Enum.IsDefined(options.MinimumLevel))
            errors.Add("Logging:MinimumLevel 必须为 Trace、Debug、Info、Warn、Error 或 Fatal。");
        if (string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            errors.Add("Logging:LogDirectory 不能为空。");
        }
        else
        {
            try { options.ResolveLogDirectory(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
            {
                errors.Add($"Logging:LogDirectory 无效：{exception.Message}");
            }
        }

        if (options.FileSizeLimitBytes <= 0) errors.Add("Logging:FileSizeLimitBytes 必须大于零。");
        if (options.RetainedFileCountLimit <= 0) errors.Add("Logging:RetainedFileCountLimit 必须大于零。");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
