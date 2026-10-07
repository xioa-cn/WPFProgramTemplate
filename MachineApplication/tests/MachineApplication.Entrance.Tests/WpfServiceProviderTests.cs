using System.Windows;
using Machine.ModuleLoad;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class WpfServiceProviderTests
{
    [Fact]
    public void StartupWindowBuilderBuildsProviderWithoutConstructingWindow()
    {
        var services = new ServiceCollection();
        var instance = new object();
        services.AddSingleton(instance);

        var provider = services.BuildWpfWithStartupWindow<Window>();
        using var lifetime = (IDisposable)provider;

        Assert.Same(provider, MainProvider.ServiceProvider);
        Assert.Same(instance, provider.GetRequiredService<object>());
        Assert.Contains(services, descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(Window));

        // 同时引用宿主和 Microsoft DI 命名空间时，标准构建方法仍无歧义。
        using var child = services.BuildServiceProvider();
        Assert.NotSame(provider, child);
        Assert.Same(provider, MainProvider.ServiceProvider);
    }
}
