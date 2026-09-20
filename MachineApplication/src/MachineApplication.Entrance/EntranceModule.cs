using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.ViewModels;
using MachineApplication.Entrance.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MachineApplication.Entrance;

public sealed class EntranceModule : IModule
{
    public void ModuleStartupCommand(StartupCommand? command = null) { }
    public void RegisterTypes(IServiceCollection services)
    {
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeView>();
        services.AddTransient<SettingsView>();
        services.AddSingleton<ThemeColorsViewModel>();
        services.AddTransient<ThemeColorsView>();
    }
    public void OnInitialized(IServiceProvider provider)
    {
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.Register("home", typeof(HomeView), "Common");
        navigation.Register("settings", typeof(SettingsView), "Common");
        navigation.Register("theme/colors", typeof(ThemeColorsView), "Common");
    }
    public void OnShutdown() { }
}
