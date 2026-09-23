using System.Windows;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.StartupTool;
using Microsoft.Extensions.DependencyInjection;
using Machine.ModuleLoad.Region;

namespace Machine.ModuleLoad;

public static class WpfApplication
{
    public static IServiceCollection Create<T>(string[]? args = null) where T : Application
    {
        CmdTools.InitializeConsole();
        GlobalLogger.Banner(
            @"     __  __         _ _  _____                      _                __          _______  ______ ");
        GlobalLogger.Banner(
            @"    |  \/  |       | (_)/ ____|                    | |               \ \        / /  __ \|  ____|");
        GlobalLogger.Banner(
            @"    | \  / |___  __| |_| |     ___  _ __  ___  ___ | | ___   ______   \ \  /\  / /| |__) | |__   ");
        GlobalLogger.Banner(
            @"    | |\/| / __|/ _` | | |    / _ \| '_ \/ __|/ _ \| |/ _ \ |______|   \ \/  \/ / |  ___/|  __|  ");
        GlobalLogger.Banner(
            @"    | |  | \__ \ (_| | | |___| (_) | | | \__ \ (_) | |  __/             \  /\  /  | |    | |     ");
        GlobalLogger.Banner(
            @"    |_|  |_|___/\__,_|_|\_____\___/|_| |_|___/\___/|_|\___|              \/  \/   |_|    |_|     ");
        GlobalLogger.Banner(
            @"                                                                                                 ");

        var serviceCollection = new ServiceCollection();


        serviceCollection.AddSingleton(new StartupCommand(args));
        serviceCollection.AddSingleton<Mapper.PermissionService>();
        serviceCollection.AddSingleton<RegionManager>();
        GlobalLogger.DebuggerLogger?.Debug("Registered region manager RegionManager.");
        serviceCollection.AddSingleton<IRegionManager>(sp => sp.GetRequiredService<RegionManager>());
        serviceCollection.AddSingleton<INavigateAsync>(sp => sp.GetRequiredService<NavigationService>());
        serviceCollection.AddSingleton<NavigationService>();
        GlobalLogger.DebuggerLogger?.Debug("Registered navigation service NavigationService.");
        serviceCollection.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        GlobalLogger.DebuggerLogger?.Debug("Registered navigation service interface INavigationService.");


        GlobalLogger.DebuggerLogger?.Info("Create a WPF desktop application through a container.");
        serviceCollection.AddSingleton<Application, T>();

        return serviceCollection;
    }

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// 注册主窗体并构建Wpf容器 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IServiceProvider BuildWpfWithStartupWindow<T>() where T : Window
        {
            serviceCollection.AddKeyedSingleton<Window, T>(Config.StartupWindow);
            return serviceCollection.BuildWpfServiceProvider();
        }

        /// <summary>注册登录与主窗口，由窗口流程服务在认证成功后创建主页。</summary>
        /// <typeparam name="TLoginWindow">实现登录成功通知契约的窗口。</typeparam>
        /// <typeparam name="TMainWindow">认证后显示的主窗口。</typeparam>
        public IServiceProvider BuildWpfWithLoginStartupWindow<TLoginWindow, TMainWindow>()
            where TLoginWindow : Window, ILoginWindow
            where TMainWindow : Window
        {
            // Window 关闭后不可再次显示，使用瞬态注册，由流程服务持有当前窗口。
            serviceCollection.AddKeyedTransient<Window, TMainWindow>(Config.StartupWindow);
            serviceCollection.AddKeyedTransient<Window, TLoginWindow>(Config.LoginWindow);
            serviceCollection.AddSingleton<LoginWindowFlow>();
            return serviceCollection.BuildWpfServiceProvider();
        }
        /// <summary>
        /// 构建Wpf容器 
        /// </summary>
        /// <returns></returns>
        public IServiceProvider BuildWpfServiceProvider()
        {
            GlobalLogger.DebuggerLogger?.Trace("Build the already prepared WPF desktop program");
            // 明确调用 Microsoft DI，避免与宿主扩展方法重名导致递归。
            MainProvider.ServiceProvider =
                ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(serviceCollection);

            return MainProvider.ServiceProvider;
        }

        [Obsolete("请使用 BuildWpfWithStartupWindow。")]
        public IServiceProvider BuildWpfOfStartup<T>() where T : Window =>
            BuildWpfWithStartupWindow<T>(serviceCollection);

        [Obsolete("请使用 BuildWpfServiceProvider。")]
        public IServiceProvider BuildWpfApp() => serviceCollection.BuildWpfServiceProvider();
    }

    /// <summary>
    /// 启动 MSDI wpf程序
    /// </summary>
    /// <param name="serviceProvider"></param>
    public static void RunWpf(this IServiceProvider serviceProvider)
    {
        var app = serviceProvider.GetRequiredService<Application>();
        (app as IWpfApp)?.InitializeWpfComponent();
        var window = serviceProvider.GetRequiredKeyedService<Window>(
            Config.StartupWindow);
        window.AssemblyUI();
        app.MainWindow = window;
        GlobalLogger.DebuggerLogger?.Success("The WPF desktop program has started successfully.");
        GlobalLogger.DebuggerLogger?.Warn("Start monitoring the desktop program....");
        ModuleProvider.RootProvider = serviceProvider;
        app.Run(window);
    }

    /// <summary>启动登录消息循环，主窗口仅在认证成功时解析和组装。</summary>
    public static void RunWpfOfLogin(this IServiceProvider serviceProvider)
    {
        var app = serviceProvider.GetRequiredService<Application>();
        (app as IWpfApp)?.InitializeWpfComponent();
        var flow = serviceProvider.GetRequiredService<LoginWindowFlow>();
        var login = flow.CreateStartupWindow();
        GlobalLogger.DebuggerLogger?.Info("The login window is ready.");
        ModuleProvider.RootProvider = serviceProvider;
        app.Run(login);
    }
}
