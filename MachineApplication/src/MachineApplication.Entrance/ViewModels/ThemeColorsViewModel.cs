using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Logger;
using MachineApplication.Entrance.Theme;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>展示 MaterialDesign 全部色盘，通过调色板 API 更新应用主色。</summary>
public sealed partial class ThemeColorsViewModel : MachineViewModelBase
{
    private readonly PaletteHelper _paletteHelper = new();
    private readonly ThemeSettingsService _themeSettings;
    private bool _initializingAdjustment = true;

    /// <summary>读取应用启动时恢复的主题参数，并生成全部 MaterialDesign 色盘。</summary>
    public ThemeColorsViewModel()
    {
        _themeSettings = (System.Windows.Application.Current as App)?.ThemeSettings ?? new ThemeSettingsService();
        ViewModelLocator.EntranceLang.PropertyChanged += OnLanguageChanged;
        Palettes = new SwatchesProvider().Swatches.Select(swatch => new ThemePalette(swatch)).ToArray();
        var currentTheme = _paletteHelper.GetTheme();
        IsDarkTheme = currentTheme.GetBaseTheme() == BaseTheme.Dark;
        IsColorAdjustmentEnabled = currentTheme.ColorAdjustment is not null;
        var adjustment = currentTheme.ColorAdjustment ?? _themeSettings.Current.CreateColorAdjustment();
        DesiredContrastRatio = adjustment.DesiredContrastRatio;
        SelectedContrast = adjustment.Contrast;
        SelectedColorSelection = adjustment.Colors;
        _initializingAdjustment = false;
        var currentColor = currentTheme.PrimaryMid.Color;
        foreach (var color in Palettes.SelectMany(palette => palette.Colors))
        {
            color.IsSelected = color.Color == currentColor;
            if (color.IsSelected) SelectedColorLabel = color.Label;
        }
    }

    public IReadOnlyList<ThemePalette> Palettes { get; }

    /// <summary>当前是否使用深色基色主题。</summary>
    [ObservableProperty]
    private bool _isDarkTheme;

    /// <summary>是否启用 MaterialDesign 的主色、辅色对比度调整。</summary>
    [ObservableProperty]
    private bool _isColorAdjustmentEnabled;

    /// <summary>期望的对比度，滑块范围为 1:1 到 21:1。</summary>
    [ObservableProperty] private double _desiredContrastRatio = 4.5;
    /// <summary>MaterialDesign 的对比度调整等级。</summary>
    [ObservableProperty] private Contrast _selectedContrast = Contrast.Medium;
    /// <summary>参与调整的颜色范围。</summary>
    [ObservableProperty] private ColorSelection _selectedColorSelection = ColorSelection.All;
    /// <summary>三点按钮控制参数弹层，点击弹层外部时自动关闭。</summary>
    [ObservableProperty] private bool _isAdjustmentSettingsOpen;

    /// <summary>打开参数；弹层负责外部点击关闭，避免关闭与反转命令发生竞态。</summary>
    [RelayCommand]
    private void OpenAdjustmentSettings() => IsAdjustmentSettingsOpen = true;

    /// <summary>对比度、等级或调整范围改变后，统一校验、应用并保存。</summary>
    partial void OnDesiredContrastRatioChanged(double value) => ApplyAdjustmentSettings();
    partial void OnSelectedContrastChanged(Contrast value) => ApplyAdjustmentSettings();
    partial void OnSelectedColorSelectionChanged(ColorSelection value) => ApplyAdjustmentSettings();

    /// <summary>关闭颜色调整时仍保存参数；初始化及回填绑定时避免递归写入。</summary>
    private void ApplyAdjustmentSettings()
    {
        if (_initializingAdjustment) return;
        UpdateTheme(_themeSettings.Current with
        {
            DesiredContrastRatio = DesiredContrastRatio,
            Contrast = SelectedContrast,
            ColorSelection = SelectedColorSelection
        });
    }

    /// <summary>截图后切换颜色调整，关闭时保留对比度等配置供下次启用。</summary>
    [RelayCommand]
    private void ToggleColorAdjustment() => UpdateTheme(_themeSettings.Current with
    {
        IsColorAdjustmentEnabled = !IsColorAdjustmentEnabled
    });

    /// <summary>通过 Result 处理主题更新和文件保存；保存失败不回滚已经显示的主题。</summary>
    private void UpdateTheme(ThemeSettings settings)
    {
        _themeSettings.Apply(settings).Match(
            applied =>
            {
                SynchronizeThemeProperties(applied);
                _themeSettings.Save().Match(_ => { }, error => GlobalLogger.Error(error));
            },
            error =>
            {
                GlobalLogger.Error(error);
                // 非法输入返回原值，不抛异常打断绑定或波纹动画。
                SynchronizeThemeProperties(_themeSettings.Current);
            });
    }

    /// <summary>将已应用参数回填到页面；使用初始化标记阻止属性回调再次保存。</summary>
    private void SynchronizeThemeProperties(ThemeSettings settings)
    {
        _initializingAdjustment = true;
        try
        {
            IsDarkTheme = settings.BaseTheme == BaseTheme.Dark;
            IsColorAdjustmentEnabled = settings.IsColorAdjustmentEnabled;
            DesiredContrastRatio = settings.DesiredContrastRatio;
            SelectedContrast = settings.Contrast;
            SelectedColorSelection = settings.ColorSelection;
            var currentColor = _paletteHelper.GetTheme().PrimaryMid.Color;
            SelectedColorLabel = string.Empty;
            foreach (var color in Palettes.SelectMany(palette => palette.Colors))
            {
                color.IsSelected = color.Color == currentColor;
                if (color.IsSelected) SelectedColorLabel = color.Label;
            }
        }
        finally
        {
            _initializingAdjustment = false;
        }
    }

    /// <summary>主题按钮显示的当前模式说明。</summary>
    public string ThemeModeLabel => IsDarkTheme
        ? ViewModelLocator.EntranceLang.ThemeColorsView_DarkTheme
        : ViewModelLocator.EntranceLang.ThemeColorsView_LightTheme;

    partial void OnIsDarkThemeChanged(bool value) => OnPropertyChanged(nameof(ThemeModeLabel));

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ThemeModeLabel));
        OnPropertyChanged(nameof(SelectedColorLabel));
        foreach (var palette in Palettes)
        {
            palette.RefreshDisplayName();
            foreach (var color in palette.Colors) color.RefreshLabel();
        }
        var selected = Palettes.SelectMany(palette => palette.Colors).FirstOrDefault(color => color.IsSelected);
        if (selected is not null) SelectedColorLabel = selected.Label;
    }

    [ObservableProperty]
    private string _selectedColorLabel = string.Empty;

    /// <summary>切换明暗基色并保存，沿用已选择的主色、辅色及调整参数。</summary>
    [RelayCommand]
    private void ToggleThemeMode() => UpdateTheme(_themeSettings.Current with
    {
        BaseTheme = IsDarkTheme ? BaseTheme.Light : BaseTheme.Dark
    });

    /// <summary>视图动画命令在截图完成后调用；换色成功后立即保存配置。</summary>
    [RelayCommand]
    private void SelectColor(ThemeColorOption? color)
    {
        if (color is null || color.IsSelected) return;
        UpdateTheme(_themeSettings.Current with { PrimaryColor = color.Color.ToString() });
    }
}
