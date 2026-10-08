using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowCore.ViewModels;
using WorkFlowCore.Views;

namespace WorkFlowCore;

public sealed class FlowModule : IModule
{
    /// <summary>保留模块启动命令入口，当前编辑器无需额外启动参数。</summary>
    public void ModuleStartupCommand(StartupCommand? command = null)
    {
    }

    /// <summary>每个页面使用独立文档状态，避免多个编辑器共享文件路径和修改标记。</summary>
    public void RegisterTypes(IServiceCollection servicesCollection)
    {
        servicesCollection.AddTransient<WorkFlowViewModel>();

        servicesCollection.AddTransient<WorkFlowPage>();
    }

    /// <summary>注册工作流页面，供主程序的路由导航访问。</summary>
    public void OnInitialized(IServiceProvider containerProvider)
    {
        var navigation = containerProvider.GetRequiredService<INavigationService>();

        navigation.Register("workflows", typeof(WorkFlowPage), "Workflows");
    }

    /// <summary>保留模块关闭入口，页面自行处理未保存文档确认。</summary>
    public void OnShutdown()
    {
    }
}
