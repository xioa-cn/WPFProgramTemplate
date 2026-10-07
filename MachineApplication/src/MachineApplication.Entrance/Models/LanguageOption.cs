using System.Globalization;

namespace MachineApplication.Entrance.Models;

/// <summary>语言菜单项，使用语言自己的名称显示，并保留完整语言标识以区分地区变体。</summary>
public sealed record LanguageOption(string Culture, bool IsSelected)
{
    public string DisplayName { get; } = GetDisplayName(Culture);

    /// <summary>标准语言使用本地名称，自定义语言标识直接显示原值，避免菜单加载失败。</summary>
    private static string GetDisplayName(string culture)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(culture).NativeName;
            return string.IsNullOrWhiteSpace(name) ? culture : $"{name} ({culture})";
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
