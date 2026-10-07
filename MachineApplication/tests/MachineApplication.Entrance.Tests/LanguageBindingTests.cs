// using System.Runtime.ExceptionServices;
// using System.Windows;
// using System.Windows.Controls;
// using System.Windows.Documents;
// using System.Windows.Threading;
// using System.Windows.Shell;
// using System.Windows.Media;
// using System.Windows.Media.Imaging;
// using System.IO;
// using Machine.ModuleLoad;
// using Machine.ModuleLoad.Region;
// using Machine.ModuleLoad.ModuleConfig;
// using Machine.ModuleLoad.Utils;
// using ModuleLoadSources.Models;
// using MachineApplication.Entrance.Theme;
// using MaterialDesignThemes.Wpf;
// using MaterialDesignColors;
// using Microsoft.Extensions.DependencyInjection;
// using I18nExtensions;
// using MachineApplication.Entrance.ViewModels;
// using MachineApplication.Entrance.Views;
// using Xunit;
//
// namespace MachineApplication.Entrance.Tests;
//
// public class LanguageBindingTests
// {
//     [Fact]
//     public void MainWindowResolvesStaticLanguageAndUpdatesTitle()
//     {
//         Exception? failure = null;
//         var thread = new Thread(() =>
//         {
//             App? app = null;
//             MainWindow? window = null;
//             IDisposable? rootLifetime = null;
//             var themeDirectory = Path.Combine(Path.GetTempPath(), "MachineThemeTests", Guid.NewGuid().ToString("N"));
//             try
//             {
//                 var themePath = Path.Combine(themeDirectory, "Theme.json");
//                 var initialTheme = new ThemeSettings
//                 {
//                     PrimaryColor = "#FF673AB7", SecondaryColor = "#FF00BCD4",
//                     DesiredContrastRatio = 6.2, Contrast = Contrast.Low, ColorSelection = ColorSelection.Secondary
//                 };
//                 Assert.True(JsonFileUtils.Write(themePath, initialTheme).IsOk);
//                 var services = new ServiceCollection();
//                 services.AddSingleton<RegionManager>();
//                 services.AddSingleton<Machine.ModuleLoad.Region.NavigationService>();
//                 services.AddSingleton<INavigationService>(provider => provider.GetRequiredService<Machine.ModuleLoad.Region.NavigationService>());
//                 services.AddSingleton(new ModuleCollection
//                 {
//                     UnitModuleName = "Common",
//                     CollectionModuleLoadMode = ModuleLoadMode.SourceGenerator,
//                     Modules = new Dictionary<string, LoadIModuleInfo>
//                     {
//                         ["Entrance"] = new() { Dll = "MachineApplication.Entrance", ModuleName = "Entrance" }
//                     }
//                 });
//                 var root = services.BuildWpfServiceProvider();
//                 rootLifetime = (IDisposable)root;
//                 app = new App(new ThemeSettingsService(themePath));
//                 app.InitializeWpfComponent();
//                 Assert.Equal(initialTheme, app.ThemeSettings.Current);
//                 Assert.Equal("#FF673AB7", new PaletteHelper().GetTheme().PrimaryMid.Color.ToString());
//                 Assert.Equal("#FF00BCD4", new PaletteHelper().GetTheme().SecondaryMid.Color.ToString());
//                 var resources = Assert.Single(app.Resources.MergedDictionaries);
//                 Assert.EndsWith("/MachineApplication.Entrance;component/AppResources.xaml", resources.Source.OriginalString);
//                 Assert.IsType<Style>(app.FindResource("MaterialDesignRaisedButton"));
//                 app.InitializeWpfComponent();
//                 Assert.Same(resources, Assert.Single(app.Resources.MergedDictionaries));
//                 root.LoadAndBuildModules();
//                 root.InitializeModules();
//                 window = new MainWindow();
//                 // 创建不可见的原生窗口句柄，让 Window.GetWindow 与装饰层具有真实窗口上下文。
//                 new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
//                 var viewModel = new MainWindowViewModel(root.GetRequiredService<INavigationService>());
//                 window.DataContext = viewModel;
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Equal("zh", LanguageManager.Instance.CurrentCulture);
//                 Assert.Equal("WPF_\u684c\u9762\u7a0b\u5e8f", window.Title);
//                 Assert.Equal(ViewModelLocator.EntranceLang.Global_AppName, window.Title);
//
//                 Assert.Equal(WindowStyle.None, window.WindowStyle);
//                 Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
//                 Assert.Equal(70, WindowChrome.GetWindowChrome(window).CaptionHeight);
//                 var pin = Assert.IsType<Button>(window.FindName("PinButton"));
//                 Assert.Same(WindowCommands.ToggleTopmost, pin.Command);
//                 pin.Command.Execute(window);
//                 Assert.True(window.Topmost);
//                 pin.Command.Execute(window);
//                 Assert.False(window.Topmost);
//                 Assert.Same(WindowCommands.Minimize, Assert.IsType<Button>(window.FindName("MinimizeButton")).Command);
//                 Assert.Same(WindowCommands.ToggleMaximize, Assert.IsType<Button>(window.FindName("MaximizeButton")).Command);
//                 Assert.Same(WindowCommands.Close, Assert.IsType<Button>(window.FindName("CloseButton")).Command);
//                 window.WindowState = WindowState.Maximized;
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Equal("还原", ((Button)window.FindName("MaximizeButton")).ToolTip);
//                 window.WindowState = WindowState.Normal;
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Equal("最大化", ((Button)window.FindName("MaximizeButton")).ToolTip);
//
//                 var surface = (FrameworkElement)window.Content;
//                 foreach (var size in new[] { new Size(1040, 640), new Size(640, 400) })
//                 {
//                     surface.Measure(size);
//                     surface.Arrange(new Rect(size));
//                     surface.UpdateLayout();
//                     var close = (Button)window.FindName("CloseButton");
//                     var bounds = close.TransformToAncestor(surface).TransformBounds(new Rect(close.RenderSize));
//                     Assert.True(bounds.Right <= size.Width && bounds.Top >= 0);
//                     Assert.True(close.ActualWidth > 0);
//                     var caption = Assert.IsType<Border>(window.FindName("ThemeTitleBar"));
//                     // UseLayoutRounding 会在非 100% DPI 下将边界对齐到物理像素。
//                     Assert.InRange(caption.ActualHeight, 69, 71);
//                     var captionButtonBounds = close.TransformToAncestor(caption).TransformBounds(new Rect(close.RenderSize));
//                     Assert.InRange(close.ActualHeight, 43, 45);
//                     Assert.InRange(captionButtonBounds.Top, 12, 14);
//                     var directory = Environment.GetEnvironmentVariable("MACHINE_WINDOW_PREVIEW");
//                     if (!string.IsNullOrWhiteSpace(directory))
//                     {
//                         Directory.CreateDirectory(directory);
//                         var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
//                         bitmap.Render(surface);
//                         var encoder = new PngBitmapEncoder();
//                         encoder.Frames.Add(BitmapFrame.Create(bitmap));
//                         using var stream = File.Create(Path.Combine(directory, $"window-{size.Width}.png"));
//                         encoder.Save(stream);
//                     }
//                 }
//
//                 var themeButton = Assert.IsType<Button>(window.FindName("ThemeButton"));
//                 Assert.Same(viewModel.ShowThemeColorsCommand, themeButton.Command);
//                 themeButton.Command.Execute(null);
//                 var themeView = Assert.IsType<ThemeColorsView>(root.GetRequiredService<RegionManager>().GetRegion("MainRegion").Content);
//                 var themeViewModel = Assert.IsType<ThemeColorsViewModel>(themeView.DataContext);
//                 Assert.Equal(6.2f, (float)themeViewModel.DesiredContrastRatio);
//                 Assert.Equal(Contrast.Low, themeViewModel.SelectedContrast);
//                 Assert.Equal(ColorSelection.Secondary, themeViewModel.SelectedColorSelection);
//                 Assert.Equal(19, themeViewModel.Palettes.Count);
//                 Assert.Equal(254, themeViewModel.Palettes.Sum(palette => palette.Colors.Count));
//                 var expectedColors = new SwatchesProvider().Swatches.SelectMany(swatch => swatch.PrimaryHues.Concat(swatch.SecondaryHues)).Select(hue => hue.Color);
//                 Assert.Equal(expectedColors.OrderBy(color => color.ToString()), themeViewModel.Palettes.SelectMany(palette => palette.Colors).Select(option => option.Color).OrderBy(color => color.ToString()));
//                 foreach (var width in new[] { 1040, 640 })
//                 {
//                     var size = new Size(width, width == 640 ? 400 : 640);
//                     surface.Measure(size);
//                     surface.Arrange(new Rect(size));
//                     surface.UpdateLayout();
//                     SavePreview(surface, $"theme-{width}");
//                 }
//
//                 surface.Measure(new Size(1040, 640));
//                 surface.Arrange(new Rect(0, 0, 1040, 640));
//                 surface.UpdateLayout();
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 var hueButton = VisualChildren<Button>(themeView).First(button => button.DataContext is ThemeColorOption color && !color.IsSelected);
//                 var chosenColor = (ThemeColorOption)hueButton.DataContext;
//                 Assert.Same(themeViewModel.SelectColorCommand, hueButton.Tag);
//                 Assert.Same(hueButton, hueButton.CommandParameter);
//                 Assert.Same(ThemeColorCommands.Apply, hueButton.Command);
//                 Assert.Same(window, Window.GetWindow(hueButton));
//                 var titleBar = Assert.IsType<Border>(window.FindName("ThemeTitleBar"));
//                 var previousTitleColor = Assert.IsType<SolidColorBrush>(titleBar.Background).Color;
//                 hueButton.Command.Execute(hueButton.CommandParameter);
//                 Assert.True(ThemeRippleAnimationHelper.GetIsThemeAnimating(window));
//                 Assert.True(chosenColor.IsSelected);
//                 Assert.False(ThemeRippleAnimationHelper.ToggleThemeWithRipple(hueButton, () => throw new InvalidOperationException("Concurrent animation accepted.")));
//                 PumpDispatcher(TimeSpan.FromMilliseconds(120));
//                 SavePreview(surface, "theme-ripple");
//                 PumpDispatcher(TimeSpan.FromSeconds(1));
//                 Assert.Equal(chosenColor.Color, new PaletteHelper().GetTheme().PrimaryMid.Color);
//                 Assert.NotEqual(previousTitleColor, Assert.IsType<SolidColorBrush>(titleBar.Background).Color);
//                 Assert.Same(app.FindResource("MaterialDesign.Brush.Primary"), titleBar.Background);
//                 SavePreview(surface, "theme-completed");
//                 Assert.False(ThemeRippleAnimationHelper.GetIsThemeAnimating(window));
//                 Assert.NotNull(app.TryFindResource("MaterialDesign.Brush.Primary"));
//                 Assert.NotNull(app.TryFindResource("MaterialDesign.Brush.Primary.Light"));
//                 Assert.NotNull(app.TryFindResource("MaterialDesign.Brush.Primary.Light.Foreground"));
//
//                 VerifyRippleRevealsNewPixels();
//
//                 var menu = Assert.IsType<MachineApplication.Entrance.Components.LeftNavMode>(window.FindName("LeftNavigation"));
//                 Assert.Same(viewModel, menu.DataContext);
//                 surface.UpdateLayout();
//                 var selectionArrow = Assert.IsType<Border>(menu.FindName("SelectionArrow"));
//                 Assert.Equal(Visibility.Visible, selectionArrow.Visibility);
//                 var arrowBounds = selectionArrow.TransformToAncestor(menu).TransformBounds(new Rect(selectionArrow.RenderSize));
//                 Assert.True(arrowBounds.Right > menu.ActualWidth);
//                 var selectionBackground = Assert.IsType<Border>(menu.FindName("SelectionBackground"));
//                 Assert.Equal(Visibility.Visible, selectionBackground.Visibility);
//                 var selectedBounds = selectionBackground.TransformToAncestor(menu).TransformBounds(new Rect(selectionBackground.RenderSize));
//                 Assert.True(selectedBounds.Right > arrowBounds.Right);
//                 Assert.True(selectedBounds.Top <= arrowBounds.Top && selectedBounds.Bottom >= arrowBounds.Bottom);
//                 Assert.IsType<SolidColorBrush>(menu.BorderBrush);
//                 Assert.NotEqual(Assert.IsType<SolidColorBrush>(window.Background).Color,
//                     Assert.IsType<SolidColorBrush>(menu.Background).Color);
//                 var settingsNode = viewModel.NavigationItems[1];
//                 var appearanceNode = settingsNode.Children[1];
//                 var themeNode = Assert.Single(appearanceNode.Children);
//                 Assert.True(settingsNode.IsExpanded);
//                 Assert.True(appearanceNode.IsExpanded);
//                 Assert.True(themeNode.IsSelected);
//                 // 从实际菜单按钮进入首页，再展开三级菜单回到主题页。
//                 var homeButton = VisualChildren<Button>(menu).Single(button => ReferenceEquals(button.CommandParameter, viewModel.NavigationItems[0]));
//                 homeButton.Command.Execute(homeButton.CommandParameter);
//                 Assert.IsType<HomeView>(root.GetRequiredService<RegionManager>().GetRegion("MainRegion").Content);
//                 Assert.True(viewModel.NavigationItems[0].IsSelected);
//                 Assert.False(themeNode.IsSelected);
//                 var groupButton = VisualChildren<Button>(menu).Single(button => ReferenceEquals(button.CommandParameter, appearanceNode));
//                 groupButton.Command.Execute(groupButton.CommandParameter);
//                 Assert.False(appearanceNode.IsExpanded);
//                 groupButton.Command.Execute(groupButton.CommandParameter);
//                 surface.UpdateLayout();
//                 var leafButton = VisualChildren<Button>(menu).Single(button => ReferenceEquals(button.CommandParameter, themeNode));
//                 leafButton.Command.Execute(leafButton.CommandParameter);
//                 Assert.IsType<ThemeColorsView>(root.GetRequiredService<RegionManager>().GetRegion("MainRegion").Content);
//                 Assert.True(themeNode.IsSelected);
//                 Assert.False(viewModel.NavigationItems[0].IsSelected);
//                 groupButton.Command.Execute(groupButton.CommandParameter);
//                 surface.UpdateLayout();
//                 Assert.Equal(Visibility.Collapsed, selectionArrow.Visibility);
//                 Assert.Equal(Visibility.Collapsed, selectionBackground.Visibility);
//                 groupButton.Command.Execute(groupButton.CommandParameter);
//                 surface.UpdateLayout();
//                 Assert.Equal(Visibility.Visible, selectionArrow.Visibility);
//                 var arrowMotion = Assert.IsType<TranslateTransform>(selectionArrow.RenderTransform);
//                 var backgroundMotion = Assert.IsType<TranslateTransform>(selectionBackground.RenderTransform);
//                 // WPF 动画时钟在下一次渲染节拍开始推进。
//                 PumpDispatcher(TimeSpan.FromMilliseconds(30));
//                 Assert.True(arrowMotion.X < 0);
//                 Assert.Equal(arrowMotion.X, backgroundMotion.X, 3);
//                 PumpDispatcher(TimeSpan.FromMilliseconds(100));
//                 var intermediateX = arrowMotion.X;
//                 Assert.True(intermediateX < 0);
//                 // 普通布局更新不能重新触发入场动画。
//                 surface.UpdateLayout();
//                 PumpDispatcher(TimeSpan.FromMilliseconds(350));
//                 // 繁忙机器上的渲染节拍可能延后，等待动画时钟完成再检查最终坐标。
//                 var animationWait = System.Diagnostics.Stopwatch.StartNew();
//                 while ((arrowMotion.HasAnimatedProperties || backgroundMotion.HasAnimatedProperties)
//                     && (arrowMotion.X != 0 || backgroundMotion.X != 0)
//                     && animationWait.Elapsed < TimeSpan.FromSeconds(2))
//                     PumpDispatcher(TimeSpan.FromMilliseconds(30));
//                 Assert.Equal(0, arrowMotion.X);
//                 Assert.Equal(0, backgroundMotion.X);
//
//                 viewModel.ChangeLanguageCommand.Execute(null);
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Equal("en", LanguageManager.Instance.CurrentCulture);
//                 Assert.Equal("WPF_Application", window.Title);
//                 Assert.Equal("Appearance", appearanceNode.Title);
//                 Assert.Equal("Theme color", themeNode.Title);
//                 var lightMenuForeground = Assert.IsType<SolidColorBrush>(menu.Foreground).Color;
//                 var selectedArrowIcon = Assert.IsType<PackIcon>(selectionArrow.Child);
//                 var selectedMenuIcon = VisualChildren<PackIcon>(leafButton).First();
//                 Assert.Same(app.FindResource("MaterialDesign.Brush.Primary.Foreground"), selectedArrowIcon.Foreground);
//                 Assert.Equal(lightMenuForeground, Assert.IsType<SolidColorBrush>(selectedMenuIcon.Foreground).Color);
//                 themeViewModel.ToggleThemeModeCommand.Execute(null);
//                 PumpDispatcher(TimeSpan.FromMilliseconds(500));
//                 Assert.NotEqual(lightMenuForeground, Assert.IsType<SolidColorBrush>(menu.Foreground).Color);
//                 var darkMenuForeground = ((SolidColorBrush)menu.Foreground).Color;
//                 Assert.Same(app.FindResource("MaterialDesign.Brush.Primary.Foreground"), selectedArrowIcon.Foreground);
//                 Assert.Equal(darkMenuForeground, Assert.IsType<SolidColorBrush>(selectedMenuIcon.Foreground).Color);
//                 Assert.NotEqual(Assert.IsType<SolidColorBrush>(window.Background).Color,
//                     Assert.IsType<SolidColorBrush>(menu.Background).Color);
//                 SavePreview(surface, "navigation-dark");
//                 // 使用真实开关绑定验证调整配置、波纹和用户所选主色。
//                 var adjustmentToggle = Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(themeView.FindName("ColorAdjustmentToggle"));
//                 adjustmentToggle.ApplyTemplate();
//                 Assert.NotNull(adjustmentToggle.Template.FindName("ThumbHolder", adjustmentToggle));
//                 var settingsButton = Assert.IsType<Button>(themeView.FindName("AdjustmentSettingsButton"));
//                 Assert.Same(themeViewModel.OpenAdjustmentSettingsCommand, settingsButton.Command);
//                 // 三点图标之外的左上角留白也必须属于按钮的可点击表面。
//                 settingsButton.ApplyTemplate();
//                 Assert.NotNull(VisualTreeHelper.HitTest(settingsButton, new Point(2, 2)));
//                 Assert.True(themeViewModel.IsColorAdjustmentEnabled);
//                 Assert.True(adjustmentToggle.IsChecked);
//                 Assert.Same(adjustmentToggle, adjustmentToggle.CommandParameter);
//                 var ratioSlider = Assert.IsType<Slider>(themeView.FindName("ContrastRatioSlider"));
//                 var contrastSelector = Assert.IsType<ComboBox>(themeView.FindName("ContrastLevelSelector"));
//                 var colorSelector = Assert.IsType<ComboBox>(themeView.FindName("AdjustmentColorSelector"));
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Same(themeViewModel, ratioSlider.DataContext);
//                 Assert.Equal(1, ratioSlider.Minimum);
//                 Assert.Equal(21, ratioSlider.Maximum);
//                 ratioSlider.SetCurrentValue(Slider.ValueProperty, 10.6);
//                 contrastSelector.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, Contrast.High);
//                 colorSelector.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, ColorSelection.Primary);
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 var configuredAdjustment = new PaletteHelper().GetTheme().ColorAdjustment;
//                 Assert.NotNull(configuredAdjustment);
//                 Assert.Equal(10.6f, configuredAdjustment.DesiredContrastRatio);
//                 Assert.Equal(Contrast.High, configuredAdjustment.Contrast);
//                 Assert.Equal(ColorSelection.Primary, configuredAdjustment.Colors);
//                 // 弹层不显示到桌面，也能离屏验证样式、文本与参数绑定。
//                 var settingsPopup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(themeView.FindName("AdjustmentSettingsPopup"));
//                 var settingsPanel = Assert.IsType<Border>(settingsPopup.Child);
//                 settingsPanel.Measure(new Size(440, 180));
//                 settingsPanel.Arrange(new Rect(0, 0, 440, 180));
//                 settingsPanel.UpdateLayout();
//                 SavePreview(settingsPanel, "color-adjustment-settings");
//                 adjustmentToggle.Command.Execute(adjustmentToggle.CommandParameter);
//                 Assert.Null(new PaletteHelper().GetTheme().ColorAdjustment);
//                 Assert.False(themeViewModel.IsColorAdjustmentEnabled);
//                 Assert.True(ThemeRippleAnimationHelper.GetIsThemeAnimating(window));
//                 PumpDispatcher(TimeSpan.FromSeconds(1));
//                 Assert.False(adjustmentToggle.IsChecked);
//                 ratioSlider.SetCurrentValue(Slider.ValueProperty, 7.2);
//                 window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
//                 Assert.Null(new PaletteHelper().GetTheme().ColorAdjustment);
//                 adjustmentToggle.Command.Execute(adjustmentToggle.CommandParameter);
//                 var adjustment = Assert.IsType<ColorAdjustment>(new PaletteHelper().GetTheme().ColorAdjustment);
//                 Assert.Equal(7.2f, adjustment.DesiredContrastRatio);
//                 Assert.Equal(Contrast.High, adjustment.Contrast);
//                 Assert.Equal(ColorSelection.Primary, adjustment.Colors);
//                 PumpDispatcher(TimeSpan.FromSeconds(1));
//                 Assert.True(adjustmentToggle.IsChecked);
//                 Assert.Equal(chosenColor.Color, new PaletteHelper().GetTheme().PrimaryMid.Color);
//                 Assert.Equal(BaseTheme.Dark, new PaletteHelper().GetTheme().GetBaseTheme());
//
//                 // 每个修改入口都写入同一份文件，包括关闭调整后编辑的参数。
//                 var saved = JsonFileUtils.Read<ThemeSettings>(themePath).Unwrap();
//                 Assert.Equal(chosenColor.Color.ToString(), saved.PrimaryColor);
//                 Assert.Equal(BaseTheme.Dark, saved.BaseTheme);
//                 Assert.Equal(7.2, saved.DesiredContrastRatio, 4);
//                 themeViewModel.ToggleColorAdjustmentCommand.Execute(null);
//                 themeViewModel.DesiredContrastRatio = 8.3;
//                 saved = JsonFileUtils.Read<ThemeSettings>(themePath).Unwrap();
//                 Assert.False(saved.IsColorAdjustmentEnabled);
//                 Assert.Equal(8.3, saved.DesiredContrastRatio, 4);
//
//                 // 重置内存主题后重新走加载路径，验证磁盘内容能恢复全部主题细节。
//                 Assert.True(app.ThemeSettings.Apply(new ThemeSettings()).IsOk);
//                 Assert.True(app.ThemeSettings.Load().IsOk);
//                 Assert.Equal(saved, app.ThemeSettings.Current);
//                 Assert.Null(new PaletteHelper().GetTheme().ColorAdjustment);
//                 Assert.Equal(BaseTheme.Dark, new PaletteHelper().GetTheme().GetBaseTheme());
//                 Assert.Equal(chosenColor.Color, new PaletteHelper().GetTheme().PrimaryMid.Color);
//                 var restoredViewModel = new ThemeColorsViewModel();
//                 Assert.False(restoredViewModel.IsColorAdjustmentEnabled);
//                 Assert.Equal(8.3f, (float)restoredViewModel.DesiredContrastRatio);
//                 restoredViewModel.ToggleColorAdjustmentCommand.Execute(null);
//                 Assert.Equal(8.3f, new PaletteHelper().GetTheme().ColorAdjustment!.DesiredContrastRatio);
//                 Assert.Equal(Contrast.High, new PaletteHelper().GetTheme().ColorAdjustment!.Contrast);
//                 Assert.Equal(ColorSelection.Primary, new PaletteHelper().GetTheme().ColorAdjustment!.Colors);
//
//                 // 非法输入返回错误并恢复绑定值，不能污染上次有效配置。
//                 var validJson = File.ReadAllText(themePath);
//                 restoredViewModel.DesiredContrastRatio = double.NaN;
//                 Assert.Equal(8.3, restoredViewModel.DesiredContrastRatio, 4);
//                 Assert.Equal(validJson, File.ReadAllText(themePath));
//                 File.WriteAllText(themePath, "{ broken JSON");
//                 Assert.True(app.ThemeSettings.Load().IsErr);
//                 Assert.Equal("{ broken JSON", File.ReadAllText(themePath));
//                 Assert.Equal(8.3f, new PaletteHelper().GetTheme().ColorAdjustment!.DesiredContrastRatio);
//
//                 // 首次启动没有配置文件时会生成默认配置。
//                 var firstRun = new ThemeSettingsService(Path.Combine(themeDirectory, "FirstRun.json"));
//                 Assert.True(firstRun.Load().IsOk);
//                 Assert.Equal(new ThemeSettings(), JsonFileUtils.Read<ThemeSettings>(firstRun.FilePath).Unwrap());
//             }
//             catch (Exception error)
//             {
//                 failure = error;
//             }
//             finally
//             {
//                 window?.Close();
//                 app?.Shutdown();
//                 rootLifetime?.Dispose();
//                 if (Directory.Exists(themeDirectory)) Directory.Delete(themeDirectory, recursive: true);
//             }
//         });
//         thread.SetApartmentState(ApartmentState.STA);
//         thread.IsBackground = true;
//         thread.Start();
//         Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "WPF binding test timed out.");
//         if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
//     }
//
//     private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
//     {
//         for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
//         {
//             var child = VisualTreeHelper.GetChild(parent, index);
//             if (child is T match) yield return match;
//             foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
//         }
//     }
//
//     // 使用两张颜色明确不同的画面验证波纹真的被绘制，而不只检查动画状态标记。
//     private static void VerifyRippleRevealsNewPixels()
//     {
//         var trigger = new Button { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left,
//             VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(40, 40, 0, 0), Opacity = 0 };
//         var content = new Grid { Background = Brushes.Red };
//         content.Children.Add(trigger);
//         var surface = new AdornerDecorator { Child = content };
//         var host = new Window { Content = surface };
//         try
//         {
//             new System.Windows.Interop.WindowInteropHelper(host).EnsureHandle();
//             surface.Measure(new Size(400, 300));
//             surface.Arrange(new Rect(0, 0, 400, 300));
//             surface.UpdateLayout();
//             PumpDispatcher(TimeSpan.FromMilliseconds(30));
//             Assert.True(ThemeRippleAnimationHelper.ToggleThemeWithRipple(trigger,
//                 () => content.Background = Brushes.Blue, pixelsPerSecond: 1000));
//             PumpDispatcher(TimeSpan.FromMilliseconds(150));
//             var ripple = Assert.Single(surface.AdornerLayer.GetAdorners(content)!.OfType<RippleEffect>());
//             Assert.True(ripple.Diameter > 0);
//             Assert.Equal(400, ripple.ActualWidth);
//             Assert.Equal(300, ripple.ActualHeight);
//             var frame = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
//             frame.Render(surface);
//             Assert.Equal(Colors.Blue, ReadPixel(frame, 50, 50));
//             Assert.Equal(Colors.Red, ReadPixel(frame, 390, 290));
//             SavePreview(surface, "ripple-pixels");
//             PumpDispatcher(TimeSpan.FromSeconds(1));
//             Assert.False(ThemeRippleAnimationHelper.GetIsThemeAnimating(host));
//             Assert.Empty(surface.AdornerLayer.GetAdorners(content) ?? []);
//             var completed = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
//             completed.Render(surface);
//             Assert.Equal(Colors.Blue, ReadPixel(completed, 390, 290));
//         }
//         finally
//         {
//             host.Close();
//         }
//     }
//
//     private static Color ReadPixel(BitmapSource bitmap, int x, int y)
//     {
//         var pixel = new byte[4];
//         bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
//         return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
//     }
//
//     private static void PumpDispatcher(TimeSpan interval)
//     {
//         var frame = new DispatcherFrame();
//         var timer = new DispatcherTimer { Interval = interval };
//         timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
//         timer.Start();
//         Dispatcher.PushFrame(frame);
//     }
//
//     private static void SavePreview(FrameworkElement surface, string name)
//     {
//         var directory = Environment.GetEnvironmentVariable("MACHINE_WINDOW_PREVIEW");
//         if (string.IsNullOrWhiteSpace(directory)) return;
//         Directory.CreateDirectory(directory);
//         var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
//         bitmap.Render(surface);
//         var encoder = new PngBitmapEncoder();
//         encoder.Frames.Add(BitmapFrame.Create(bitmap));
//         using var stream = File.Create(Path.Combine(directory, $"{name}.png"));
//         encoder.Save(stream);
//     }
// }
