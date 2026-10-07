using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace WorkFlowCore;

public sealed class FlowModule : IModule
{
    public void ModuleStartupCommand(StartupCommand? command = null)
    {
    }

    public void RegisterTypes(IServiceCollection servicesCollection)
    {
    }

    public void OnInitialized(IServiceProvider containerProvider)
    {
    }

    public void OnShutdown()
    {
    }
}