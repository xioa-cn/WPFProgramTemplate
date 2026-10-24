using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Mvvm;

public partial class MainWindowSettings : ViewModelBase {
    [ObservableProperty] private double _height;
    [ObservableProperty] private double _width;

    public const string SectionName = "MainWindow";
}

public static class MainWindowSettingsExtensions {
    public static IServiceCollection AddWindowSettings(this IServiceCollection services) {
        services.AddOptions<MainWindowSettings>().BindConfiguration(MainWindowSettings.SectionName);
        return services;
    }
}