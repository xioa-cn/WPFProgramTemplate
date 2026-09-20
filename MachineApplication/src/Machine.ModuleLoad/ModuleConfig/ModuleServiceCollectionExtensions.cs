using System.Windows;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.ModuleConfig;

/// <summary>为模块服务集合桥接应用基础服务。</summary>
public static class ModuleServiceCollectionExtensions
{
    private static readonly Type[] InfrastructureServiceTypes =
    [
        typeof(StartupCommand),
        typeof(Application),
        typeof(RegionManager),
        typeof(NavigationService),
        typeof(INavigationService)
    ];

    /// <summary>将主容器已注册的基础单例实例共享给模块子容器。</summary>
    /// <param name="services">尚未构建的模块服务集合。</param>
    /// <param name="rootProvider">拥有基础服务的应用主容器。</param>
    /// <returns>传入的模块服务集合。</returns>
    /// <remarks>
    /// 在模块 RegisterTypes 之后、BuildServiceProvider 之前调用。
    /// 保留模块显式注册的同类型服务；未注册的主容器服务会跳过。
    /// 基础服务列表仅包含应用单例，不自动共享 Scoped 或 Transient 服务。
    /// </remarks>
    public static IServiceCollection AddRootInfrastructureServices(
        this IServiceCollection services, IServiceProvider rootProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rootProvider);

        foreach (var serviceType in InfrastructureServiceTypes)
        {
            if (services.Any(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == serviceType))
                continue;

            try
            {
                var instance = rootProvider.GetService(serviceType);
                if (instance is null)
                {
                    GlobalLogger.DebuggerLogger?.Debug($"Infrastructure service '{serviceType.Name}' is not registered in the root container; skipping bridge.");
                    continue;
                }

                // 使用实例注册，避免子容器释放主容器拥有的 IDisposable 服务。
                // 不注册根 IServiceProvider，确保构造函数中的 IServiceProvider 仍代表子容器。
                services.AddSingleton(serviceType, instance);
                GlobalLogger.DebuggerLogger?.Debug($"Bridged root infrastructure service '{serviceType.Name}' into the module container.");
            }
            catch (Exception exception)
            {
                GlobalLogger.Error($"Failed to bridge infrastructure service '{serviceType.FullName}'.", exception);
                throw;
            }
        }

        return services;
    }
}
