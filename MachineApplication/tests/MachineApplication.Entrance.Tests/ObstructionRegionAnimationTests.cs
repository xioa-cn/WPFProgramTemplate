using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class ObstructionRegionAnimationTests
{
    [Fact]
    public void CoverIsOpaqueAndDoesNotReplaceNavigationContent() => Sta(() =>
    {
        using var rig = new Rig();
        var page = new Border { Background = Brushes.Red };
        rig.Manager.Navigate("main", page);
        rig.Layout();

        Assert.Same(page, rig.Host.Content);
        var cover = Assert.Single(rig.Covers);
        Assert.True(cover.IsHitTestVisible);
        var bitmap = new RenderTargetBitmap(320, 200, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(rig.Root);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(10, 10, 1, 1), pixel, 4, 0);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixel);
        Assert.Equal(1, rig.Host.Opacity);
        Assert.Null(rig.Host.Clip);
    });

    [Fact]
    public void CompletionRevealsTargetAndRemovesAnimation() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Animation.Duration = TimeSpan.FromMilliseconds(30);
        var target = new Border();
        rig.Manager.Navigate("main", target);
        var cover = Assert.Single(rig.Covers);
        PumpUntil(() => rig.Covers.Length == 0);
        Assert.False(cover.HasAnimatedProperties);
        Assert.Same(target, rig.Host.Content);
    });

    [Fact]
    public void RapidNavigationAndDeactivationCleanUpPreviousCover() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Manager.Navigate("main", new Border());
        var previousCover = Assert.Single(rig.Covers);
        var target = new Border();
        rig.Manager.Navigate("main", target);
        Assert.NotSame(previousCover, Assert.Single(rig.Covers));
        Assert.False(previousCover.HasAnimatedProperties);
        Assert.Same(target, rig.Host.Content);
        rig.Manager.Regions["main"].Deactivate(target);
        Assert.Empty(rig.Covers);
        Assert.Null(rig.Host.Content);
    });

    [Theory]
    [InlineData(false, 600)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public void DisabledAnimationShowsTargetImmediately(bool enabled, int milliseconds) => Sta(() =>
    {
        using var rig = new Rig();
        rig.Animation.IsEnabled = enabled;
        rig.Animation.Duration = TimeSpan.FromMilliseconds(milliseconds);
        var target = new Border();
        rig.Manager.Navigate("main", target);
        Assert.Empty(rig.Covers);
        Assert.Same(target, rig.Host.Content);
    });

    [Fact]
    public void UnloadAndDisposalRemoveCover() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Manager.Navigate("main", new Border());
        var cover = Assert.Single(rig.Covers);
        rig.Host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Empty(rig.Covers);
        Assert.False(cover.HasAnimatedProperties);
        rig.Manager.Navigate("main", new Border());
        Assert.Single(rig.Covers);
        rig.Manager.Dispose();
        Assert.Empty(rig.Covers);
    });

    [Fact]
    public void CoverTracksHostSize() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Manager.Navigate("main", new Border());
        rig.Layout(640, 480);
        Assert.Equal(new Size(640, 480), Assert.Single(rig.Covers).RenderSize);
    });

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (condition() || DateTime.UtcNow >= deadline) frame.Continue = false;
        };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(condition(), "Animation did not finish.");
    }

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Rig : IDisposable
    {
        private readonly ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();
        public ContentControl Host { get; } = new()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        public ObstructionRegionAnimation Animation { get; } = new()
        {
            Duration = TimeSpan.FromSeconds(10), Background = Brushes.White
        };
        public AdornerDecorator Root { get; }
        public RegionManager Manager { get; }
        public Adorner[] Covers => Root.AdornerLayer.GetAdorners(Host) ?? [];

        public Rig()
        {
            Root = new AdornerDecorator { Child = Host };
            Manager = new RegionManager(_provider);
            Manager.Register("main", Host, Animation);
            Layout();
        }

        public void Layout(double width = 320, double height = 200)
        {
            Root.Measure(new Size(width, height));
            Root.Arrange(new Rect(0, 0, width, height));
            Root.UpdateLayout();
        }

        public void Dispose()
        {
            Manager.Dispose();
            _provider.Dispose();
        }
    }
}
