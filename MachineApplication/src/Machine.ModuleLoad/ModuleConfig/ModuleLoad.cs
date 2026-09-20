using Machine.ModuleLoad.Logger;
using Microsoft.Extensions.DependencyInjection;
using ModuleLoadSources.Models;

namespace Machine.ModuleLoad.ModuleConfig;

public static class ModuleLoad
{
    public static IServiceCollection RegisterConfigModule(this IServiceCollection serviceCollection,
        ModuleLoadSources.Models.ModuleConfig moduleConfig)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(moduleConfig);

        foreach (var unitModule in moduleConfig.UnitModules ?? [])
        {
            if (string.IsNullOrWhiteSpace(unitModule.Name))
                throw new InvalidOperationException("Each unitModules entry must define a Name attribute.");

            var moduleCollection = new ModuleCollection
            {
                UnitModuleName = unitModule.Name,
                CollectionModuleLoadMode = unitModule.LoadMode,
                Modules = new Dictionary<string, LoadIModuleInfo>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var dllModule in unitModule.DllModules ?? [])
            {
                if (string.IsNullOrWhiteSpace(dllModule.Name))
                    throw new InvalidOperationException($"Unit module '{unitModule.Name}' contains a dllModule without a Name attribute.");
                if (string.IsNullOrWhiteSpace(dllModule.Dll))
                    throw new InvalidOperationException($"Module '{dllModule.Name}' in unit '{unitModule.Name}' must define a Dll attribute.");
                if (moduleCollection.Modules.ContainsKey(dllModule.Name))
                    throw new InvalidOperationException($"Unit module '{unitModule.Name}' contains duplicate module name '{dllModule.Name}'.");

                moduleCollection.Modules.Add(dllModule.Name, new LoadIModuleInfo
                {
                    ModuleName = dllModule.Name,
                    Dll = dllModule.Dll,
                    Path = dllModule.Path,
                    Module = null
                });
            }

            GlobalLogger.DebuggerLogger?.Success($"Detected configured unit module '{unitModule.Name}'.");
            
            serviceCollection.AddSingleton(moduleCollection);
        }

        return serviceCollection;
    }
}
