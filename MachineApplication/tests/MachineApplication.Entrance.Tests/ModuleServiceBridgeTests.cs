using System.Windows;
using StartupCommand = Machine.ModuleLoad.StartupCommand;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class ModuleServiceBridgeTests
{
    [Fact]
    public void ConstructorInjectionUsesRootInstancesAndChildProvider()
    {
        var rootServices = new ServiceCollection();
        rootServices.AddSingleton(new StartupCommand(["--demo"]));
        rootServices.AddSingleton<RegionManager>();
        rootServices.AddSingleton<NavigationService>();
        rootServices.AddSingleton<INavigationService>(provider => provider.GetRequiredService<NavigationService>());
        using var root = rootServices.BuildServiceProvider();
        var services = new ServiceCollection();
        services.AddTransient<Consumer>();
        services.AddRootInfrastructureServices(root);
        services.AddRootInfrastructureServices(root);
        using var child = services.BuildServiceProvider();

        var consumer = child.GetRequiredService<Consumer>();
        Assert.Same(root.GetRequiredService<INavigationService>(), consumer.Navigation);
        Assert.Same(root.GetRequiredService<StartupCommand>(), consumer.Command);
        Assert.Same(root.GetRequiredService<RegionManager>(), child.GetRequiredService<RegionManager>());
        Assert.Same(consumer.Navigation, child.GetRequiredService<NavigationService>());
        Assert.Same(child.GetRequiredService<IServiceProvider>(), consumer.Provider);
        Assert.NotSame(root.GetRequiredService<IServiceProvider>(), consumer.Provider);
        Assert.Single(child.GetServices<INavigationService>());
    }

    [Fact]
    public void DisposingChildDoesNotDisposeRootOwnedService()
    {
        var rootServices = new ServiceCollection();
        rootServices.AddSingleton<INavigationService>(_ => new DisposableNavigation());
        var root = rootServices.BuildServiceProvider();
        var navigation = (DisposableNavigation)root.GetRequiredService<INavigationService>();
        var services = new ServiceCollection();
        services.AddRootInfrastructureServices(root);
        using (var child = services.BuildServiceProvider())
            Assert.Same(navigation, child.GetRequiredService<INavigationService>());
        Assert.False(navigation.Disposed);
        root.Dispose();
        Assert.True(navigation.Disposed);
    }

    [Fact]
    public void ExplicitModuleRegistrationIsPreserved()
    {
        using var root = new ServiceCollection().AddSingleton(new StartupCommand(["root"])).BuildServiceProvider();
        var localCommand = new StartupCommand(["module"]);
        var services = new ServiceCollection();
        services.AddSingleton(localCommand);
        services.AddRootInfrastructureServices(root);
        using var child = services.BuildServiceProvider();
        Assert.Same(localCommand, child.GetRequiredService<StartupCommand>());
    }

    public sealed class Consumer(INavigationService navigation, StartupCommand command, IServiceProvider provider)
    {
        public INavigationService Navigation { get; } = navigation;
        public StartupCommand Command { get; } = command;
        public IServiceProvider Provider { get; } = provider;
    }

    private sealed class DisposableNavigation : INavigationService, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;

        public void RequestNavigate(string regionName, Uri target, Action<NavigationResult>? callback = null,
            NavigationParameters? navigationParameters = null)
        {
            
        }

        public void Register(string url, Type viewType, string? moduleName = null) => throw new NotSupportedException();
        public UIElement Navigate(string regionName, string url, bool keepAlive = true) => throw new NotSupportedException();
        public bool CanNavigate(string url) => false;
        public bool GoBack(string regionName) => false;
    }
}
