using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class RegionPageTests
{
    [Fact]
    public void PagesAndUserControlsCanSwitchReuseAndGoBack()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? testWindow = null;
            try
            {
                // Instantiate the actual compiled icon template on the UI thread:
                // compilation alone cannot detect a default TwoWay binding without a path.
                var settings = new MachineApplication.Entrance.Views.RouterSetting(null!);
                IEnumerable<DependencyObject> Descendants(DependencyObject root)
                {
                    yield return root;
                    foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                        foreach (var descendant in Descendants(child)) yield return descendant;
                }
                var picker = Descendants(settings).OfType<ComboBox>().Single(combo => combo.IsEditable);
                var iconTemplate = (StackPanel)picker.ItemTemplate.LoadContent();
                iconTemplate.DataContext = MaterialDesignThemes.Wpf.PackIconKind.Palette;
                var bindingPump = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => bindingPump.Continue = false));
                Dispatcher.PushFrame(bindingPump);
                iconTemplate.Measure(new Size(400, 60));
                var icon = iconTemplate.Children.OfType<MaterialDesignThemes.Wpf.PackIcon>().Single();
                Assert.Equal(MaterialDesignThemes.Wpf.PackIconKind.Palette, icon.Kind);
                Assert.Equal("Palette", iconTemplate.Children.OfType<TextBlock>().Single().Text);
                using var provider = new ServiceCollection().BuildServiceProvider();
                var manager = new RegionManager(provider);
                var host = new ContentControl();
                manager.Register("main", host);
                var home = new UserControl();
                var first = new Page();
                var second = new Page();
                void Layout()
                {
                    var pump = new DispatcherFrame();
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                        new Action(() => pump.Continue = false));
                    Dispatcher.PushFrame(pump);
                    host.Measure(new Size(800, 600));
                    host.Arrange(new Rect(0, 0, 800, 600));
                    host.UpdateLayout();
                }
                settings.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MachineApplication.Entrance;component/AppResources.xaml")
                });
                picker.ItemsSource = Enum.GetValues<MaterialDesignThemes.Wpf.PackIconKind>().Distinct().ToArray();
                manager.Navigate("main", settings);
                Layout();
                testWindow = new Window { Content = host, Width = 1000, Height = 800, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
                testWindow.Show();
                Layout();
                picker.IsDropDownOpen = true;
                Layout();
                var realized = Enumerable.Range(0, picker.Items.Count)
                    .Count(index => picker.ItemContainerGenerator.ContainerFromIndex(index) is not null);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                picker.IsDropDownOpen = false;
                manager.Navigate("main", home);
                Layout();
                Console.WriteLine($"Icon count={picker.Items.Count}; realized={realized}; leave={timer.ElapsedMilliseconds} ms; CanContentScroll={ScrollViewer.GetCanContentScroll(picker)}");
                manager.Navigate("main", home);
                Assert.Same(home, host.Content);
                manager.Navigate("main", first);
                Layout();
                var firstFrame = Assert.IsType<Frame>(host.Content);
                Assert.Same(first, firstFrame.Content);
                // Native Frame navigation must not desynchronize the region.
                firstFrame.Navigate(new Page());
                Layout();
                Assert.Same(first, firstFrame.Content);
                firstFrame.Navigate(new Uri("https://example.invalid/blocked"));
                Layout();
                Assert.Same(first, firstFrame.Content);
                Assert.False(firstFrame.CanGoBack);
                Assert.False(firstFrame.CanGoForward);
                firstFrame.Refresh();
                Layout();
                Assert.Same(first, firstFrame.Content);
                manager.Navigate("main", second);
                Layout();
                Assert.Same(second, Assert.IsType<Frame>(host.Content).Content);
                Assert.True(manager.GoBack("main"));
                Layout();
                Assert.Same(firstFrame, host.Content);
                Assert.True(manager.GoBack("main"));
                Assert.Same(home, host.Content);
                Assert.True(manager.GoBack("main"));
                Assert.Same(settings, Assert.IsType<Frame>(host.Content).Content);
                Assert.False(manager.GoBack("main"));
                manager.Navigate("main", first);
                Layout();
                Assert.Same(firstFrame, host.Content);
                manager.Clear("main");
                Assert.Null(host.Content);
                manager.Navigate("main", first);
                Layout();
                Assert.Same(first, Assert.IsType<Frame>(host.Content).Content);
            }
            catch (Exception ex) { failure = ex; }
            finally { testWindow?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}