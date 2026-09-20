using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance;
using MachineApplication.Entrance.Views;

namespace MachineApplication.Startup;

public static class Program
{
    /// <summary>应用程序入口：初始化 WPF 应用并启动消息循环。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var wpfApplication = WpfApplication.Create<App>();
        wpfApplication.LoadModuleConfig(AppDomain.CurrentDomain.BaseDirectory);
        var wpfServiceProvider = wpfApplication.BuildWpfWithStartupWindow<MainWindow>();
        wpfServiceProvider.LoadAndBuildModules();
        wpfServiceProvider.InitializeModules();
        wpfServiceProvider.RunWpf();
    }
}
