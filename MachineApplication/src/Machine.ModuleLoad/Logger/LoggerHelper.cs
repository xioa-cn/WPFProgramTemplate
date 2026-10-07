using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Machine.ModuleLoad.Logger;

public static class LoggerHelper
{
    public static string ClaLog(this string className, string message)
    {
        return $"[{className}] {message}";
    }

    public static IServiceCollection AddLogger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => !descriptor.IsKeyedService &&
                                       descriptor.ServiceType == typeof(GlobalLoggerAdapter)))
            return services;

        services.TryAddSingleton<IConfiguration>(_ => new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appSettings.json", optional: true, reloadOnChange: false)
            .Build());
        services.AddOptions<LoggingOptions>().BindConfiguration(LoggingOptions.SectionName);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<LoggingOptions>>(new LoggingOptionsValidator()));
        services.TryAddSingleton<GlobalLoggerAdapter>();
        services.TryAddSingleton<ILogger>(provider => provider.GetRequiredService<GlobalLoggerAdapter>());
        return services;
    }
}
