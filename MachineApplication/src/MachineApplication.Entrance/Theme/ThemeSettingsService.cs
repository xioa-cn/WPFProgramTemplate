using System.IO;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Utils;
using MaterialDesignThemes.Wpf;
using RestSharp;

namespace MachineApplication.Entrance.Theme;

/// <summary>负责主题配置的加载、校验、应用和保存；应用主题的方法应在 WPF UI 线程调用。</summary>
public sealed class ThemeSettingsService
{
    private readonly PaletteHelper _paletteHelper = new();

    /// <summary>默认使用 exe 同级的 Theme.json；测试可传入独立路径，避免污染实际配置。</summary>
    public ThemeSettingsService(string? filePath = null) => FilePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "Theme.json");

    /// <summary>当前配置文件路径，不受工作目录变化影响。</summary>
    public string FilePath { get; }
    /// <summary>当前已应用的参数，包含关闭颜色调整后仍需保留的值。</summary>
    public ThemeSettings Current { get; private set; } = new();

    /// <summary>资源字典就绪后、窗口创建前加载主题；首次启动生成默认文件，损坏文件保留供检查。</summary>
    public Result<ThemeSettings, string> Load()
    {
        var existed = File.Exists(FilePath);
        return JsonFileUtils.Read(FilePath, () => new ThemeSettings())
            .AndThen(Apply)
            .AndThen(settings => existed ? Result<ThemeSettings, string>.Ok(settings) : Save().Map(_ => settings))
            .Map(settings =>
            {
                GlobalLogger.DebuggerLogger?.Debug($"Loaded theme settings from '{FilePath}'.");
                return settings;
            });
    }

    /// <summary>先完整校验再应用主题；成功后更新内存配置，调用方随后决定何时保存。</summary>
    public Result<ThemeSettings, string> Apply(ThemeSettings settings) => settings.Validate()
        .AndThen(valid => ThemeSettings.ParseColor(valid.PrimaryColor)
            .AndThen(primary => ThemeSettings.ParseColor(valid.SecondaryColor).AndThen(secondary =>
            {
                try
                {
                    var theme = _paletteHelper.GetTheme();
                    theme.SetBaseTheme(valid.BaseTheme);
                    theme.SetPrimaryColor(primary);
                    theme.SetSecondaryColor(secondary);
                    theme.ColorAdjustment = valid.IsColorAdjustmentEnabled ? valid.CreateColorAdjustment() : null;
                    _paletteHelper.SetTheme(theme);
                    Current = valid;
                    GlobalLogger.DebuggerLogger?.Debug($"Applied theme settings: base={valid.BaseTheme}, primary={valid.PrimaryColor}, adjustment={valid.IsColorAdjustmentEnabled}.");
                    return Result<ThemeSettings, string>.Ok(valid);
                }
                catch (Exception exception)
                {
                    var error = $"Failed to apply theme settings: {exception.Message}";
                    GlobalLogger.Error(error, exception);
                    return Result<ThemeSettings, string>.Err(error);
                }
            })));

    /// <summary>保存当前已应用的参数；失败返回错误，保留内存主题和上次有效的磁盘文件。</summary>
    public Result<string, string> Save() => Current.Validate()
        .AndThen(settings => JsonFileUtils.Write(FilePath, settings))
        .Map(path =>
        {
            GlobalLogger.DebuggerLogger?.Debug($"Saved theme settings to '{path}'.");
            return path;
        });
}
