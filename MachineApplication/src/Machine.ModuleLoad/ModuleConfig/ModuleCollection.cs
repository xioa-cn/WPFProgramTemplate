using System.Runtime.Loader;
using ModuleLoadSources.Models;

namespace Machine.ModuleLoad.ModuleConfig;

public class ModuleCollection
{
    public string UnitModuleName { get; set; } = string.Empty;
    public ModuleLoadMode CollectionModuleLoadMode { get; set; }
    public Dictionary<string, LoadIModuleInfo> Modules { get; set; }
    public IServiceProvider? ChildServiceProvider { get; internal set; }
    internal AssemblyLoadContext? LoadContext { get; set; }
}

public class LoadIModuleInfo
{
    public string ModuleName { get; set; } = string.Empty;
    public string Dll { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public IModule? Module { get; set; }
}
