using System.IO;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Region;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.ModuleConfig;

/// <summary>提供独立 DLL 插件的热加载、卸载和重载。</summary>
public static class ModuleHotLoad
{
    private static readonly Dictionary<string, (IModule Module, IServiceProvider Provider, ModuleAssemblyLoadContext Context)> HotModules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>从指定 DLL 加载指定 IModule，并完成完整启动流程。</summary>
    public static IServiceProvider Load(IServiceProvider rootProvider, string unitModuleName, string moduleName, string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitModuleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        var path = Path.GetFullPath(assemblyPath);
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        GlobalLogger.DebuggerLogger?.Info($"Hot-loading module '{moduleName}' from '{path}'.");
        var context = new ModuleAssemblyLoadContext(unitModuleName, Path.GetDirectoryName(path)!);
        var assembly = context.LoadFromAssemblyPath(path);
        var type = assembly.GetTypes().FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && (t.Name == moduleName || t.FullName == moduleName)) ?? throw new InvalidOperationException($"找不到 IModule: {moduleName}");
        var module = (IModule?)Activator.CreateInstance(type) ?? throw new InvalidOperationException($"无法创建模块: {type.FullName}");
        var childServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        module.RegisterTypes(childServices);
        childServices.AddRootInfrastructureServices(rootProvider);
        var child = childServices.BuildServiceProvider();
        module.ModuleStartupCommand();
        module.OnInitialized(child);
        HotModules[unitModuleName] = (module, child, context);
        ModuleProvider.Register(unitModuleName, child);
        GlobalLogger.DebuggerLogger?.Success($"Module '{moduleName}' hot-loaded successfully.");
        return child;
    }

    /// <summary>卸载指定热加载插件。</summary>
    public static void Unload(IServiceProvider rootProvider, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        if (!HotModules.Remove(moduleName, out var hot)) throw new KeyNotFoundException(moduleName);
        try { hot.Module.OnShutdown(); } catch (Exception ex) { GlobalLogger.Error($"Failed to shut down module '{moduleName}'.", ex); }
        (hot.Provider as IDisposable)?.Dispose(); ModuleProvider.Remove(moduleName); hot.Context.Unload();
        GlobalLogger.DebuggerLogger?.Success($"Unload requested for module '{moduleName}'.");
    }

    /// <summary>卸载并重新加载指定插件。</summary>
    public static IServiceProvider Reload(IServiceProvider rootProvider, string unitModuleName, string moduleName, string assemblyPath)
    {
        try { Unload(rootProvider, unitModuleName); } catch (KeyNotFoundException) { }
        return Load(rootProvider, unitModuleName, moduleName, assemblyPath);
    }
}
