using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Machine.ModuleLoad.Mvvm;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class RegionNavigationTests
{
    [Fact]
    public void ContextMergesQueryAndObjectsAndReferencesActualRegion() => Sta(() =>
    {
        using var rig = new Rig();
        var payload = new object();
        var input = new NavigationParameters { { "id", 42 }, { "payload", payload } };
        NavigationResult? result = null;
        rig.Manager.RequestNavigate("main", "edit?id=1&title=%E4%B8%AD%E6%96%87&tag=a&tag=b#section",
            r => result = r, input);
        Assert.True(result!.Result);
        var context = result.Context!;
        Assert.Same(rig.Region, context.Region);
        Assert.Same(rig.Region.NavigationService, context.NavigationService);
        Assert.Same(context, ((Screen)rig.Host.Content).Model.LastTo);
        Assert.Equal("main", context.RegionName);
        Assert.Equal(42, context.Parameters.GetValue<int>("id"));
        Assert.Equal("中文", context.Parameters.GetValue<string>("title"));
        Assert.Equal(new[] { "a", "b" }, context.Parameters.GetValues<string>("tag"));
        Assert.Same(payload, context.Parameters.GetValue<object>("payload"));
        Assert.False(input.ContainsKey("title"));
        Assert.Contains("#section", context.Uri.OriginalString);
    });

    [Fact]
    public void ConfirmationWaitsForViewAndModelAndDenialHasNoSideEffects() => Sta(() =>
    {
        using var rig = new Rig();
        var initial = rig.Navigate("edit");
        Action<bool>? first = null;
        Action<bool>? second = null;
        initial.Confirm = (_, callback) => first = callback;
        initial.Model.Confirm = (_, callback) => second = callback;
        NavigationResult? result = null;
        var created = rig.Created.Count;
        rig.Manager.RequestNavigate("main", "other", r => result = r);
        Assert.Null(result);
        Assert.Null(second);
        Assert.Same(initial, rig.Host.Content);
        first!(true);
        Assert.NotNull(second);
        second!(false);
        Assert.False(result!.Result);
        Assert.Null(result.Error);
        Assert.Equal(created, rig.Created.Count);
        Assert.Equal(0, initial.Model.FromCount);
        Assert.Same(initial, rig.Host.Content);
        Assert.False(rig.Journal.CanGoBack);
    });

    [Fact]
    public void BackgroundConfirmationAndDuplicateCallbackCompleteExactlyOnce() => Sta(() =>
    {
        using var rig = new Rig();
        var initial = rig.Navigate("edit");
        Action<bool>? continuation = null;
        initial.Model.Confirm = (_, callback) => continuation = callback;
        var completed = 0;
        var uiThread = Environment.CurrentManagedThreadId;
        rig.Manager.RequestNavigate("main", "other", result =>
        {
            Assert.True(result.Result);
            Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
            completed++;
        });
        Task.Run(() => { continuation!(true); continuation(false); }).GetAwaiter().GetResult();
        Assert.Same(initial, rig.Host.Content);
        Pump();
        Assert.IsType<OtherScreen>(rig.Host.Content);
        Assert.Equal(1, completed);
        Assert.Equal(1, initial.Model.FromCount);
    });

    [Fact]
    public void LatestRequestSupersedesPendingConfirmationWithoutRevivingOldRequest() => Sta(() =>
    {
        using var rig = new Rig();
        var initial = rig.Navigate("edit");
        var confirmations = new List<Action<bool>>();
        initial.Model.Confirm = (_, callback) => confirmations.Add(callback);
        var results = new List<NavigationResult>();
        rig.Manager.RequestNavigate("main", "other", results.Add);
        rig.Manager.RequestNavigate("main", "edit?id=2", results.Add);
        Assert.Single(results);
        Assert.False(results[0].Result);
        confirmations[0](true);
        Assert.Same(initial, rig.Host.Content);
        confirmations[1](true);
        Assert.Equal(2, results.Count);
        Assert.True(results[1].Result);
        Assert.Equal(2, initial.Model.LastTo!.Parameters.GetValue<int>("id"));
        Assert.DoesNotContain(rig.Created, x => x is OtherScreen);
    });

    [Fact]
    public void DeferredExceptionReportsFailureAndLeavesHistoryAndViewIntact() => Sta(() =>
    {
        using var rig = new Rig();
        var initial = rig.Navigate("edit");
        Action<bool>? confirm = null;
        initial.Model.Confirm = (_, callback) => confirm = callback;
        var errors = 0;
        rig.Region.NavigationService.NavigationFailed += (_, args) => { Assert.NotNull(args.Error); errors++; };
        NavigationResult? result = null;
        rig.Manager.RequestNavigate("main", "broken", r => result = r);
        confirm!(true);
        Assert.False(result!.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(1, errors);
        Assert.Same(initial, rig.Host.Content);
        Assert.Equal(0, initial.Model.FromCount);
        Assert.False(rig.Journal.CanGoBack);
    });

    [Fact]
    public void JournalReplaysParametersSupportsForwardAndTruncatesBranches() => Sta(() =>
    {
        using var rig = new Rig();
        var edit = rig.Navigate("edit?id=7");
        var other = rig.Navigate("other?mode=2");
        Assert.True(rig.Journal.CanGoBack);
        rig.Journal.GoBack();
        Assert.Same(edit, rig.Host.Content);
        Assert.Equal(7, edit.Model.LastTo!.Parameters.GetValue<int>("id"));
        Assert.Equal("edit?id=7", rig.Journal.CurrentEntry!.Uri.OriginalString);
        Assert.True(rig.Journal.CanGoForward);
        rig.Journal.GoForward();
        Assert.Same(other, rig.Host.Content);
        Assert.Equal(2, other.Model.LastTo!.Parameters.GetValue<int>("mode"));
        rig.Journal.GoBack();
        rig.Navigate("edit?id=9");
        Assert.False(rig.Journal.CanGoForward);
        Assert.Empty(rig.Journal.ForwardStack);
        Assert.Equal(9, edit.Model.LastTo!.Parameters.GetValue<int>("id"));
    });

    [Fact]
    public void JournalDoesNotMoveUntilConfirmationSucceeds() => Sta(() =>
    {
        using var rig = new Rig();
        var edit = rig.Navigate("edit");
        var other = rig.Navigate("other");
        Action<bool>? confirm = null;
        other.Model.Confirm = (_, callback) => confirm = callback;
        rig.Journal.GoBack();
        Assert.Same(other, rig.Host.Content);
        Assert.Equal("other", rig.Journal.CurrentEntry!.Uri.OriginalString);
        confirm!(false);
        Assert.Equal("other", rig.Journal.CurrentEntry.Uri.OriginalString);
        Assert.False(rig.Journal.CanGoForward);
        rig.Journal.GoBack();
        confirm!(true);
        Assert.Same(edit, rig.Host.Content);
        Assert.Equal("edit", rig.Journal.CurrentEntry.Uri.OriginalString);
    });

    [Fact]
    public void KeepAliveFalseReleasesMemberAndRecreatesItOnBackNavigation() => Sta(() =>
    {
        using var rig = new Rig();
        var edit = rig.Navigate("edit?id=17");
        edit.Model.KeepAlive = false;
        rig.Navigate("other");
        Assert.DoesNotContain(edit, rig.Region.Views);
        Assert.Single(rig.Region.Views);
        rig.Journal.GoBack();
        var recreated = Assert.IsType<Screen>(rig.Host.Content);
        Assert.NotSame(edit, recreated);
        Assert.Equal(17, recreated.Model.LastTo!.Parameters.GetValue<int>("id"));
        Assert.Equal(3, rig.Created.Count);
    });

    [Fact]
    public void LifetimeOnViewTakesPrecedenceOverDataContext() => Sta(() =>
    {
        using var rig = new Rig();
        var retained = new LifetimeScreen { KeepAlive = true };
        retained.Model.KeepAlive = false;
        rig.Manager.Navigate("main", retained);
        rig.Navigate("other");
        Assert.Contains(retained, rig.Region.Views);
        retained.KeepAlive = false;
        rig.Region.Activate(retained);
        rig.Region.Deactivate(retained);
        Assert.DoesNotContain(retained, rig.Region.Views);
    });

    [Fact]
    public void ReuseRejectionCreatesNewInstanceAndBackFindsTheCorrectInstance() => Sta(() =>
    {
        using var rig = new Rig();
        var one = rig.Navigate("edit?id=1");
        one.Model.MatchId = true;
        var two = rig.Navigate("edit?id=2");
        two.Model.MatchId = true;
        Assert.NotSame(one, two);
        Assert.Equal(2, rig.Region.Views.Count);
        rig.Journal.GoBack();
        Assert.Same(one, rig.Host.Content);
        rig.Journal.GoForward();
        Assert.Same(two, rig.Host.Content);
    });

    [Fact]
    public void SameInstanceViewAndDataContextReceiveOnlyOneConfirmation() => Sta(() =>
    {
        using var rig = new Rig();
        var self = new Screen();
        self.DataContext = self;
        var confirmations = 0;
        self.Confirm = (_, callback) => { confirmations++; callback(true); };
        rig.Manager.Navigate("main", self);
        rig.Navigate("other");
        Assert.Equal(1, confirmations);
    });

    [Fact]
    public void RegionCollectionsDiscoveryAndScopesAreLiveAndIsolated() => Sta(() =>
    {
        using var rig = new Rig();
        var regions = rig.Manager.Regions;
        rig.Manager.RegisterViewWithRegion("tools", typeof(ToolScreen));
        var host = new ContentControl();
        rig.Manager.Register("tools", host);
        Assert.Equal(2, regions.Count);
        Assert.IsType<ToolScreen>(host.Content);
        rig.Manager.RegisterViewWithRegion("tools", () => new OtherScreen());
        Assert.Equal(2, regions["tools"].Views.Count);
        var child = rig.Manager.CreateRegionManager();
        try
        {
            var childHost = new ContentControl();
            child.Register("main", childHost);
            child.RequestNavigate("main", "edit?id=3");
            Assert.IsType<Screen>(childHost.Content);
            Assert.Null(rig.Host.Content);
            Assert.NotSame(child.Regions["main"].NavigationService.Journal, rig.Journal);
            Assert.Single(child.Regions["main"].Views);
        }
        finally { ((IDisposable)child).Dispose(); }
        Assert.True(regions.Remove("tools"));
        Assert.Null(host.Content);
        Assert.Single(regions);
    });

    [Fact]
    public void UnregisteredDiscoveredViewFailsInsteadOfBeingConstructedImplicitly() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Manager.RegisterViewWithRegion("tools", typeof(UnregisteredScreen));
        var host = new ContentControl();
        var error = Assert.Throws<InvalidOperationException>(() => rig.Manager.Register("tools", host));
        Assert.Contains(nameof(UnregisteredDependency), error.Message, StringComparison.Ordinal);
        Assert.Null(host.Content);
    });

    [Fact]
    public void ChildRegionManagerResolvesDiscoveredViewsFromItsOwnScope() => Sta(() =>
    {
        using var provider = new ServiceCollection()
            .AddScoped<ScopedScreen>()
            .BuildServiceProvider();
        using var parent = new RegionManager(provider);
        using var child = (IDisposable)parent.CreateRegionManager();
        var childManager = (IRegionManager)child;
        var parentHost = new ContentControl();
        var childHost = new ContentControl();

        parent.RegisterViewWithRegion("parent", typeof(ScopedScreen));
        parent.Register("parent", parentHost);
        childManager.RegisterViewWithRegion("child", typeof(ScopedScreen));
        childManager.Register("child", childHost);

        Assert.NotSame(parentHost.Content, childHost.Content);
    });

    [Fact]
    public void DirectRegionActivationChangesHostAndClosedPagesAreRemovedFromHistory() => Sta(() =>
    {
        using var rig = new Rig();
        var first = rig.Navigate("edit");
        var second = rig.Navigate("other");
        rig.Region.Activate(first);
        Assert.Same(first, rig.Host.Content);
        Assert.Single(rig.Region.ActiveViews);
        rig.Region.Activate(second);
        rig.Routes.ClosePage("main", "edit?ignored=1");
        Assert.DoesNotContain(first, rig.Region.Views);
        Assert.False(rig.Journal.CanGoBack);
        rig.Journal.Clear();
        Assert.Null(rig.Journal.CurrentEntry);
        Assert.False(rig.Journal.CanGoForward);
    });

    [Fact]
    public void PageHostsRemainStableAndNativeFrameNavigationIsBlocked() => Sta(() =>
    {
        using var rig = new Rig();
        var first = new Page();
        var second = new Page();
        rig.Manager.Navigate("main", first);
        Pump();
        var frame = Assert.IsType<Frame>(rig.Host.Content);
        rig.Manager.Navigate("main", second);
        Pump();
        Assert.True(rig.Manager.GoBack("main"));
        Assert.Same(frame, rig.Host.Content);
        frame.Navigate(new Page());
        Pump();
        Assert.Same(first, frame.Content);
        Assert.False(frame.CanGoBack);
        Assert.True(rig.Manager.GoForward("main"));
        Assert.Same(second, Assert.IsType<Frame>(rig.Host.Content).Content);
    });

    [Fact]
    public void ClearInvalidatesDelayedNavigationAndItsCompletionOnlyRunsOnce() => Sta(() =>
    {
        using var rig = new Rig();
        var initial = rig.Navigate("edit");
        Action<bool>? confirm = null;
        initial.Model.Confirm = (_, callback) => confirm = callback;
        var results = new List<NavigationResult>();
        rig.Manager.RequestNavigate("main", "other", results.Add);
        rig.Manager.Clear("main");
        confirm!(true);
        Assert.Null(rig.Host.Content);
        Assert.Single(results);
        Assert.False(results[0].Result);
        Assert.Null(rig.Journal.CurrentEntry);
    });

    [Fact]
    public void ParameterConversionAndQueryDecodingPreserveDuplicatesAndFragments()
    {
        var parameters = NavigationParameters.Parse(new Uri("https://example.org/edit?a=1&a=2&q=a+b&encoded=%26%3D&flag#x?bad=1"));
        Assert.Equal(new int?[] { 1, 2 }, parameters.GetValues<int?>("a"));
        Assert.Equal("a b", parameters.GetValue<string>("q"));
        Assert.Equal("&=", parameters.GetValue<string>("encoded"));
        Assert.Equal("", parameters.GetValue<string>("flag"));
        Assert.False(parameters.ContainsKey("bad"));
        Assert.Equal(99, parameters.GetValueOrDefault("absent", 99));
        Assert.Throws<KeyNotFoundException>(() => parameters.GetValue<int>("absent"));
    }

    [Fact]
    public void FailureCallbackThrowIsNotReportedTwice() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Navigate("edit");
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() =>
            rig.Manager.RequestNavigate("main", "missing", _ => { calls++; throw new InvalidOperationException("observer"); }));
        Assert.Equal(1, calls);
    });

    [Fact]
    public void TaskNavigationCompletesAfterConfirmation() => Sta(() =>
    {
        using var rig = new Rig();
        var first = rig.Navigate("edit");
        Action<bool>? confirm = null;
        first.Model.Confirm = (_, callback) => confirm = callback;
        var task = rig.Region.NavigationService.RequestNavigateAsync(new Uri("other", UriKind.Relative));
        Assert.False(task.IsCompleted);
        confirm!(true);
        Assert.True(task.GetAwaiter().GetResult().Result);
    });

    [Fact]
    public void RegionAnimationAttachedPropertyIsAppliedBeforeAndAfterRegionRegistration() => Sta(() =>
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var manager = new RegionManager(provider);

        var configuredBeforeRegistration = new ContentControl();
        RegionManager.SetRegionManager(configuredBeforeRegistration, manager);
        var firstAnimation = new RegionAnimation { Duration = TimeSpan.FromSeconds(5.25) };
        RegionAnimation.SetAnimation(configuredBeforeRegistration, firstAnimation);
        manager.Register("before", configuredBeforeRegistration);
        Assert.Same(firstAnimation, manager.Regions["before"].Animation);

        var configuredAfterRegistration = new ContentControl();
        RegionManager.SetRegionManager(configuredAfterRegistration, manager);
        manager.Register("after", configuredAfterRegistration);
        var secondAnimation = new RegionAnimation { Duration = TimeSpan.FromSeconds(5.25) };
        RegionAnimation.SetAnimation(configuredAfterRegistration, secondAnimation);
        Assert.Same(secondAnimation, manager.Regions["after"].Animation);
    });

    [Fact]
    public void RegionAnimationXamlPropertyElementPreservesCustomDuration() => Sta(() =>
    {
        const string markup = """
            <ContentControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            xmlns:region="clr-namespace:Machine.ModuleLoad.Region;assembly=Machine.ModuleLoad">
                <region:RegionAnimation.Animation>
                    <region:RegionAnimation Duration="0:0:5.25" IsEnabled="True">
                        <region:RegionAnimation.EasingFunction>
                            <CubicEase EasingMode="EaseOut" />
                        </region:RegionAnimation.EasingFunction>
                    </region:RegionAnimation>
                </region:RegionAnimation.Animation>
            </ContentControl>
            """;

        var host = Assert.IsType<ContentControl>(XamlReader.Parse(markup));
        var animation = Assert.IsType<RegionAnimation>(RegionAnimation.GetAnimation(host));
        Assert.Equal(TimeSpan.FromSeconds(5.25), animation.Duration);
        Assert.True(animation.IsEnabled);
        Assert.IsType<CubicEase>(animation.EasingFunction);
    });

    [Fact]
    public void RegionAnimationStartsWhenRegionContentChanges() => Sta(() =>
    {
        using var rig = new Rig();
        rig.Region.Animation = new RegionAnimation { Duration = TimeSpan.FromSeconds(5.25) };

        rig.Navigate("edit");
        Assert.True(rig.Host.HasAnimatedProperties);
        rig.Navigate("other");
        Assert.True(rig.Host.HasAnimatedProperties);
    });

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Rig : IDisposable
    {
        private readonly ServiceProvider _provider;
        public RegionManager Manager { get; }
        public NavigationService Routes { get; }
        public ContentControl Host { get; } = new();
        public IRegion Region => Manager.Regions["main"];
        public IRegionNavigationJournal Journal => Region.NavigationService.Journal;
        public List<Screen> Created { get; } = [];
        public Rig()
        {
            var services = new ServiceCollection();
            services.AddTransient<Screen>(_ => { var view = new Screen(); Created.Add(view); return view; });
            services.AddTransient<OtherScreen>(_ => { var view = new OtherScreen(); Created.Add(view); return view; });
            services.AddTransient<ToolScreen>(_ => { var view = new ToolScreen(); Created.Add(view); return view; });
            services.AddTransient<UnregisteredScreen>();
            services.AddTransient<BrokenScreen>(_ => throw new InvalidOperationException("broken factory"));
            _provider = services.BuildServiceProvider();
            Manager = new(_provider);
            Routes = new(_provider, Manager);
            Routes.Register("edit", typeof(Screen));
            Routes.Register("other", typeof(OtherScreen));
            Routes.Register("broken", typeof(BrokenScreen));
            Manager.Register("main", Host);
        }
        public Screen Navigate(string target)
        {
            NavigationResult? completed = null;
            Manager.RequestNavigate("main", target, result => completed = result);
            Assert.True(completed?.Result, completed?.Error?.ToString());
            return Assert.IsAssignableFrom<Screen>(Host.Content);
        }
        public void Dispose() { Manager.Dispose(); _provider.Dispose(); }
    }

    public class Model : NavigationObservableObject, IRegionMemberLifetime
    {
        public bool KeepAlive { get; set; } = true;
        public bool MatchId { get; set; }
        public RegionNavigationContext? LastTo { get; private set; }
        public RegionNavigationContext? LastFrom { get; private set; }
        public int FromCount { get; private set; }
        public Action<RegionNavigationContext, Action<bool>>? Confirm { get; set; }
        public override bool IsNavigationTarget(RegionNavigationContext context) => !MatchId ||
            context.Parameters.GetValueOrDefault<int>("id") == LastTo!.Parameters.GetValueOrDefault<int>("id");
        public override void OnNavigatedTo(RegionNavigationContext context) => LastTo = context;
        public override void OnNavigatedFrom(RegionNavigationContext context) { FromCount++; LastFrom = context; }
        public override void ConfirmNavigationRequest(RegionNavigationContext context, Action<bool> callback)
        {
            if (Confirm is null) callback(true);
            else Confirm(context, callback);
        }
    }

    public class Screen : UserControl, IConfirmNavigationRequest
    {
        public Model Model { get; } = new();
        public Action<RegionNavigationContext, Action<bool>>? Confirm { get; set; }
        public Screen() => DataContext = Model;
        public bool IsNavigationTarget(RegionNavigationContext context) => true;
        public void OnNavigatedTo(RegionNavigationContext context) { }
        public void ConfirmNavigationRequest(RegionNavigationContext context, Action<bool> callback)
        {
            if (Confirm is null) callback(true);
            else Confirm(context, callback);
        }
    }
    public class OtherScreen : Screen { }
    public class BrokenScreen : Screen { }
    public class ToolScreen : Screen { }
    public sealed class UnregisteredDependency { }
    public sealed class UnregisteredScreen : UserControl
    {
        public UnregisteredScreen(UnregisteredDependency dependency) { }
    }
    public sealed class ScopedScreen : UserControl { }
    public class LifetimeScreen : Screen, IRegionMemberLifetime { public bool KeepAlive { get; set; } }
}
