using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance;
using MachineApplication.Entrance.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MachineApplication.Startup;

public static class Program
{
    /// <summary>以登录窗口启动应用，认证通过后才创建主窗口。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        WpfApplication.Create<App>()
            .LoadModuleConfig(AppDomain.CurrentDomain.BaseDirectory)
            .BuildWpfWithLoginStartupWindow<LoginWindow, MainWindow>()
            .LoadAndBuildModules()
            .InitializeModules()
            .RunWpfOfLogin();
    }
}