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
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<UserManagement>();
        services.AddTransient<PermissionSettingsViewModel>();
        services.AddTransient<PermissionSettings>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<RouterSettingViewModel>();
        services.AddTransient<RouterSetting>();
        services.AddTransient<HomeView>();
        services.AddTransient<SettingsView>();
        services.AddSingleton<ThemeColorsViewModel>();
        services.AddTransient<ThemeColorsView>();
    }
    public void OnInitialized(IServiceProvider provider)
    {
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.Register("settings/routes", typeof(RouterSetting), "Common");
        navigation.Register("settings/users", typeof(UserManagement), "Common");
        navigation.Register("settings/permissions", typeof(PermissionSettings), "Common");
       
        navigation.Register("home", typeof(HomeView), "Common");
        navigation.Register("settings", typeof(SettingsView), "Common");
        navigation.Register("theme/colors", typeof(ThemeColorsView), "Common");
    }
    public void OnShutdown() { }
}
