using Machine.ModuleLoad.Logger;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ModuleLoadSources.Models;
using Machine.ModuleLoad.Region;

namespace Machine.ModuleLoad.ModuleConfig;

/// <summary>
/// 提供模块配置加载、模块展开和模块启动的扩展方法。
/// </summary>
public static class LoadModuleExtensions
{
    /// <summary>读取模块 XML 配置并将模块集合注册到根服务集合。</summary>
    /// <param name="services">应用根服务集合。</param>
    /// <param name="configPath">配置文件所在目录。</param>
    /// <param name="configName">配置文件名称，默认是 modules.xml。</param>
    /// <returns>传入的服务集合。</returns>
    public static IServiceCollection LoadModuleConfig(this IServiceCollection services, string? configPath = null,
        string configName = "modules.xml")
    {
        // 读取并解析模块 XML 配置。
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        LoadConfigUtils.LoadFromFile(configPath, configName)
            .Match(m => services.RegisterConfigModule(m), e => GlobalLogger.Error(e));
        return services;
    }

    /// <summary>加载并实例化所有模块，统一注册模块服务并创建单元模块子容器。</summary>
    /// <param name="provider">应用根服务提供程序。</param>
    /// <returns>传入的根服务提供程序。</returns>
    public static IServiceProvider AnalyzeExpandModule(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        // 先实例化全部模块，再统一注册服务并创建各单元的子容器。
        foreach (var collection in provider.GetServices<ModuleCollection>())
        {
            // 子容器继承根容器，因此模块可以使用应用级服务。
            var childServices = new ServiceCollection();
            // 一个单元模块使用一个加载上下文，保证其依赖程序集能够从模块目录解析。
            foreach (var info in collection.Modules.Values)
            {
                if (info.Module is not null)
                    continue;
                // 根据配置加载程序集并查找 IModule 实现。
                var assembly = LoadModuleAssembly(collection, info);
                var type = assembly.GetTypes().FirstOrDefault(t =>
                               typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                           ?? throw new InvalidOperationException(
                               $"Assembly '{info.Dll}' does not contain an IModule implementation.");
                info.Module = Activator.CreateInstance(type) as IModule
                              ?? throw new InvalidOperationException($"Unable to create module '{type.FullName}'.");
            }

            // 所有模块实例化完成后再执行注册，避免注册阶段缺少模块依赖。
            foreach (var module in collection.Modules.Values.Select(x => x.Module).OfType<IModule>())
                module.RegisterTypes(childServices);
            // 保存子容器，供启动阶段和 ModuleProvider 查询。
            childServices.AddRootInfrastructureServices(provider, collection.UnitModuleName);
            collection.ChildServiceProvider = childServices.BuildServiceProvider();
        }
        ModuleProvider.Initialize(provider);
        return provider;
    }

    /// <summary>加载模块程序集、注册模块服务并构建模块子容器。</summary>
    public static IServiceProvider LoadAndBuildModules(this IServiceProvider provider) => provider.AnalyzeExpandModule();

    /// <summary>按模块生命周期执行启动命令和初始化回调。</summary>
    /// <param name="provider">已完成模块展开的应用根服务提供程序。</param>
    /// <returns>传入的根服务提供程序。</returns>
    public static IServiceProvider StartupModule(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
       
        foreach (var collection in provider.GetServices<ModuleCollection>())
        {
            var modules = collection.Modules.Values.Select(x => x.Module).OfType<IModule>().ToArray();
            // 先统一执行所有启动命令。
            foreach (var module in modules) module.ModuleStartupCommand();
            var child = collection.ChildServiceProvider ?? provider;
            // 启动命令全部完成后，再执行初始化回调。
            foreach (var module in modules) module.OnInitialized(child);
        }

        return provider;
    }

    /// <summary>执行所有模块的启动命令和初始化回调。</summary>
    public static IServiceProvider InitializeModules(this IServiceProvider provider) => provider.StartupModule();

    /// <summary>根据模块路径和程序集名称解析 DLL 的绝对路径。</summary>
    /// <param name="modulePath">配置中的模块路径。</param>
    /// <param name="dllName">程序集名称。</param>
    /// <returns>程序集绝对路径。</returns>
    /// <exception cref="FileNotFoundException">程序集文件不存在时抛出。</exception>
    private static string ResolveAssemblyPath(string modulePath, string dllName)
    {
        // 相对路径以应用程序目录为基准，并自动补全 DLL 扩展名。
        var fileName = Path.GetExtension(dllName).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            ? dllName
            : dllName + ".dll";
        var basePath = string.IsNullOrWhiteSpace(modulePath) || modulePath == "."
            ? AppContext.BaseDirectory
            : Path.GetFullPath(modulePath, AppContext.BaseDirectory);
        var path = Path.Combine(basePath, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException($"Module assembly was not found: {path}", path);
        return path;
    }

    /// <summary>根据单元模块的加载模式获取程序集。</summary>
    private static Assembly LoadModuleAssembly(ModuleCollection collection, LoadIModuleInfo info)
    {
        if (collection.CollectionModuleLoadMode == ModuleLoadMode.SourceGenerator)
        {
            var name = info.Dll.TrimEnd(".dll".ToCharArray());
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
            if (assembly is null)
            {
                try { assembly = Assembly.Load(new AssemblyName(name)); }
                catch (Exception ex)
                {
                    GlobalLogger.Error($"Failed to load SourceGenerator module assembly '{info.Dll}'.", ex);
                }
            }
            if (assembly is null)
            {
                var message = $"SourceGenerator module assembly '{info.Dll}' has not been loaded.";
                GlobalLogger.Error(message);
                throw new InvalidOperationException(message);
            }
            GlobalLogger.DebuggerLogger?.Debug($"Using referenced module assembly: {info.Dll}");
            return assembly;
        }

        var path = ResolveAssemblyPath(info.Path, info.Dll);
        collection.LoadContext ??= new ModuleAssemblyLoadContext(
            collection.UnitModuleName, Path.GetDirectoryName(path)!);
        GlobalLogger.DebuggerLogger?.Debug($"Loading module assembly from file: {path}");
        return collection.LoadContext.LoadFromAssemblyPath(path);
    }
}

/// <summary>
/// 模块程序集加载上下文。
/// 模块自己的依赖从模块 DLL 所在目录解析；宿主契约程序集继续使用默认上下文，
/// 确保模块中的 IModule 与宿主看到的是同一个类型。
/// </summary>
internal sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _moduleDirectory;

    public ModuleAssemblyLoadContext(string name, string moduleDirectory) : base($"Module:{name}", isCollectible: true)
    {
        _moduleDirectory = moduleDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 这些程序集定义了宿主与模块之间的公共类型，必须共享默认上下文中的实例。
        if (assemblyName.Name is "Machine.ModuleLoad" or "ModuleLoadSources" or "RestSharp")
            return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

        if (assemblyName.Name is null)
            return null;

        var dependencyPath = Path.Combine(_moduleDirectory, assemblyName.Name + ".dll");
        return File.Exists(dependencyPath) ? LoadFromAssemblyPath(dependencyPath) : null;
    }
}
