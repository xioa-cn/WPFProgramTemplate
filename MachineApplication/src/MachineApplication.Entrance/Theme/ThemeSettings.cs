using System.Globalization;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using RestSharp;

namespace MachineApplication.Entrance.Theme;

/// <summary>Theme.json 的主题配置；颜色存为 ARGB 文本，关闭颜色调整时仍保留其细节参数。</summary>
public sealed record ThemeSettings
{
    /// <summary>明暗基色主题。</summary>
    public BaseTheme BaseTheme { get; init; } = BaseTheme.Light;
    /// <summary>主色，格式为 #RRGGBB 或 #AARRGGBB。</summary>
    public string PrimaryColor { get; init; } = "#FF2196F3";
    /// <summary>辅色，格式为 #RRGGBB 或 #AARRGGBB。</summary>
    public string SecondaryColor { get; init; } = "#FFFF9800";
    /// <summary>是否启用 MaterialDesign 颜色调整。</summary>
    public bool IsColorAdjustmentEnabled { get; init; } = true;
    /// <summary>期望对比度，合法范围为 1 到 21。</summary>
    public double DesiredContrastRatio { get; init; } = 4.5;
    /// <summary>对比度调整等级。</summary>
    public Contrast Contrast { get; init; } = Contrast.Medium;
    /// <summary>参与颜色调整的主色、辅色范围。</summary>
    public ColorSelection ColorSelection { get; init; } = ColorSelection.All;

    /// <summary>校验手工编辑的配置，所有字段合法后才允许更新主题。</summary>
    public Result<ThemeSettings, string> Validate()
    {
        if (!Enum.IsDefined(BaseTheme) || !Enum.IsDefined(Contrast) || !Enum.IsDefined(ColorSelection))
            return Result<ThemeSettings, string>.Err("Theme settings contain an invalid enum value.");
        if (!double.IsFinite(DesiredContrastRatio) || DesiredContrastRatio is < 1 or > 21)
            return Result<ThemeSettings, string>.Err("Theme contrast ratio must be between 1 and 21.");
        return ParseColor(PrimaryColor).AndThen(_ => ParseColor(SecondaryColor)).Map(_ => this);
    }

    /// <summary>解析十六进制颜色，非法文本返回错误，避免通过 ColorConverter 抛异常。</summary>
    internal static Result<Color, string> ParseColor(string? text)
    {
        if (text is null || text.Length is not (7 or 9) || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var argb))
            return Result<Color, string>.Err($"Invalid theme color '{text}'. Expected #RRGGBB or #AARRGGBB.");
        return Result<Color, string>.Ok(Color.FromArgb(text.Length == 7 ? (byte)255 : (byte)(argb >> 24),
            (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }

    /// <summary>创建新的库配置实例，避免共享可变对象导致开关关闭后丢失参数。</summary>
    public ColorAdjustment CreateColorAdjustment() => new()
    {
        DesiredContrastRatio = (float)DesiredContrastRatio,
        Contrast = Contrast,
        Colors = ColorSelection
    };
}
