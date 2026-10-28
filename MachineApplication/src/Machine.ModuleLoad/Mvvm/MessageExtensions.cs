using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Mvvm;

public static class MessageExtensions {
    public static IServiceCollection AddMessageControl(this IServiceCollection services) {
        services.AddSingleton<ILoadingBar, LoadingBar>();
        services.AddSingleton<ISnackBar, Snack>();
        return services;
    }
}