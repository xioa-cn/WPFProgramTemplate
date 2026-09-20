using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignColors;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Theme;

/// <summary>色盘中的单个色阶，保留 MaterialDesign 提供的前景色以保证对比度。</summary>
public sealed partial class ThemeColorOption : ObservableObject
{
    /// <summary>根据 MaterialDesign 色阶创建可绑定的颜色和说明。</summary>
    public ThemeColorOption(string paletteName, Hue hue)
    {
        // 库中的名称包含 Primary/Secondary 前缀，页面使用标准色阶编号。
        Name = hue.Name.Replace("Primary", string.Empty, StringComparison.Ordinal)
            .Replace("Secondary", "A", StringComparison.Ordinal);
        Color = hue.Color;
        Hex = $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        PaletteName = paletteName;
        Background = new SolidColorBrush(Color);
        Foreground = new SolidColorBrush(hue.Foreground);
        Background.Freeze();
        Foreground.Freeze();
    }

    public string Name { get; }
    public string Hex { get; }
    public string PaletteName { get; }
    public string Label => $"{ViewModelLocator.EntranceLang.GetValue($"ThemeColorsView_Palettes_{PaletteName.ToLowerInvariant()}")} {Name} - {Hex}";
    /// <summary>通知语言切换后刷新色阶提示文本。</summary>
    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
    public Color Color { get; }
    public SolidColorBrush Background { get; }
    public SolidColorBrush Foreground { get; }

    [ObservableProperty]
    private bool _isSelected;
}
