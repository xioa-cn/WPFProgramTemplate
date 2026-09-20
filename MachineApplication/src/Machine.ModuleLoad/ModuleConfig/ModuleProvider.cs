using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.ModuleConfig;

/// <summary>
/// 管理并提供按单元模块名称索引的模块子容器。
/// </summary>
public static class ModuleProvider
{
    private static readonly object SyncRoot = new();
    private static Dictionary<string, IServiceProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>从根容器收集已构建的模块子容器并建立名称索引。</summary>
    /// <param name="rootProvider">应用根服务提供程序。</param>
    internal static void Initialize(IServiceProvider rootProvider)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        // 将已构建的子容器按单元模块名称建立索引。
        var providers = rootProvider.GetServices<ModuleCollection>()
            .Where(x => x.ChildServiceProvider is not null)
            .ToDictionary(x => x.UnitModuleName, x => x.ChildServiceProvider!, StringComparer.OrdinalIgnoreCase);
        lock (SyncRoot)
            _providers = providers;
    }

    internal static void Remove(string moduleName)
    {
        lock (SyncRoot)
            _providers.Remove(moduleName);
    }

    internal static void Register(string moduleName, IServiceProvider provider)
    {
        lock (SyncRoot)
            _providers[moduleName] = provider;
    }

    /// <summary>按单元模块名称获取对应的子容器。</summary>
    /// <param name="moduleName">单元模块名称。</param>
    /// <returns>模块子容器。</returns>
    /// <exception cref="KeyNotFoundException">找不到指定模块或模块尚未展开时抛出。</exception>
    public static IServiceProvider GetModuleProvider(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        // 名称匹配不区分大小写，避免配置大小写差异导致查找失败。
        lock (SyncRoot)
        {
            if (_providers.TryGetValue(moduleName, out var provider))
                return provider;
        }
        throw new KeyNotFoundException($"Unit module provider '{moduleName}' was not found. Call AnalyzeExpandModule first.");
    }

    /// <summary>从指定单元模块的子容器中获取必需服务。</summary>
    /// <typeparam name="T">服务类型。</typeparam>
    /// <param name="moduleName">单元模块名称。</param>
    /// <returns>已注册的服务实例。</returns>
    public static T GetRequiredService<T>(string moduleName) where T : notnull =>
        GetModuleProvider(moduleName).GetRequiredService<T>();
}
