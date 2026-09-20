using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.ModuleConfig;

public interface IModule
{
    void ModuleStartupCommand(StartupCommand? command = null);
    void RegisterTypes(IServiceCollection servicesCollection);
    void OnInitialized(IServiceProvider containerProvider);
    void OnShutdown();
}
