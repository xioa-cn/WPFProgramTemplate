using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Logger;
using Microsoft.Extensions.DependencyInjection;
using AIAgentFrameworkModule.Agent;
using AIAgentFrameworkModule.Services;
using AIAgentFrameworkModule.ViewModels;
using AIAgentFrameworkModule.Views;
using Machine.ModuleLoad.Region;
using System.Windows;

namespace AIAgentFrameworkModule;

/// <summary>接入现有模块系统；启动时不会自动连接模型或产生费用。</summary>
public sealed class AgentFrameworkModule : IModule
{
    private AgentDebugViewModel? debugWorkspace;
    private AgentGroupChatViewModel? groupWorkspace;

    /// <summary>模块启动命令入口，目前不需要额外处理。</summary>
    /// <param name="command">宿主传入的启动参数。</param>
    public void ModuleStartupCommand(StartupCommand? command = null)
    {
        GlobalLogger.DebuggerLogger?.Debug("[AI.Module] 收到模块启动命令；模块不做额外初始化，也不会自动连接模型。");
    }

    /// <param name="servicesCollection">Microsoft.Extensions.DependencyInjection 提供的注册集合。</param>
    public void RegisterTypes(IServiceCollection servicesCollection)
    {
        // AddSingleton 复用同一个工厂，不会让不同用户共享聊天历史。
        servicesCollection.AddSingleton<AgentCore>();
        // 调试工具的两个页面共享一个工作区；不是面向多用户的生产聊天单例。
        servicesCollection.AddSingleton<AgentProfileStore>();
        servicesCollection.AddSingleton<AgentDebugViewModel>();
        servicesCollection.AddSingleton<IAgentStepExecutor, AgentStepExecutor>();
        servicesCollection.AddSingleton<AgentGroupChatStore>();
        servicesCollection.AddSingleton<AgentGroupChatViewModel>();
        servicesCollection.AddTransient<AgentGroupChatPage>();
        servicesCollection.AddTransient<AgentGroupChatConfigPage>();
        // AddTransient 每次解析创建页面，页面状态由上面的工作区统一维护。
        servicesCollection.AddTransient<AgentModelConfigPage>();
        servicesCollection.AddTransient<AgentChatPage>();
        GlobalLogger.DebuggerLogger?.Debug(
            "[AI.Module] 已注册模型工厂、调试工作区、群聊工作区与四个页面。");
    }

    /// <param name="containerProvider">宿主提供的服务容器。</param>
    public void OnInitialized(IServiceProvider containerProvider)
    {
        // INavigationService 是宿主的导航服务；Register 把 URL 关联到模块子容器中的页面。
        INavigationService navigation = containerProvider.GetRequiredService<INavigationService>();
        navigation.Register("ai/models", typeof(AgentModelConfigPage), "AIAgent");
        navigation.Register("ai/chat", typeof(AgentChatPage), "AIAgent");
        navigation.Register("ai/workflow", typeof(AgentGroupChatPage), "AIAgent");
        navigation.Register("ai/group-chat/settings", typeof(AgentGroupChatConfigPage), "AIAgent");
        GlobalLogger.Info("[AI.Module] 已注册模型、对话、群聊和群聊配置路由；初始化不连接模型。");
        // 只读取本地配置，不连接模型；模块卸载或应用退出时释放这个调试工作区。
        debugWorkspace = containerProvider.GetRequiredService<AgentDebugViewModel>();
        groupWorkspace = containerProvider.GetRequiredService<AgentGroupChatViewModel>();
        GlobalLogger.DebuggerLogger?.Debug("[AI.Module] 模块初始化完成，调试工作区已由宿主容器创建。");
        if (Application.Current != null) Application.Current.Exit += OnApplicationExit;
    }

    /// <summary>模块退出时停止未完成请求并清理密钥；Dispose 可以重复调用。</summary>
    public void OnShutdown()
    {
        GlobalLogger.DebuggerLogger?.Debug("[AI.Module] 模块退出：停止未完成请求并清除内存中的密钥。");
        if (Application.Current != null) Application.Current.Exit -= OnApplicationExit;
        groupWorkspace?.Dispose();
        groupWorkspace = null;
        debugWorkspace?.Dispose();
        debugWorkspace = null;
    }

    /// <summary>WPF Exit 事件用于宿主未显式卸载模块时的资源清理。</summary>
    private void OnApplicationExit(object sender, ExitEventArgs args)
    {
        OnShutdown();
    }
}
