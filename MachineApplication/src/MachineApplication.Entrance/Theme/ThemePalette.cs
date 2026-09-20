using MaterialDesignColors;
using CommunityToolkit.Mvvm.ComponentModel;
using I18nExtensions;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Theme;

/// <summary>完整的 MaterialDesign 色系，包含普通色阶及可用的强调色阶。</summary>
public sealed partial class ThemePalette : ObservableObject
{
    /// <summary>普通色阶从浅到深排列，随后显示强调色阶。</summary>
    public ThemePalette(Swatch swatch)
    {
        Name = swatch.Name;
        Colors = swatch.PrimaryHues.Concat(swatch.SecondaryHues)
            .Select(hue => new ThemeColorOption(Name, hue))
            .OrderBy(color => color.Name.StartsWith('A'))
            .ThenBy(color => int.Parse(color.Name.TrimStart('A'), System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }

    public string Name { get; }
    /// <summary>当前语言下显示的色系名称。</summary>
    public string DisplayName => ViewModelLocator.EntranceLang.GetValue($"ThemeColorsView_Palettes_{Name.ToLowerInvariant()}");
    /// <summary>通知设计器和页面刷新当前语言的色系名称。</summary>
    public void RefreshDisplayName() => OnPropertyChanged(nameof(DisplayName));
    public IReadOnlyList<ThemeColorOption> Colors { get; }
}
