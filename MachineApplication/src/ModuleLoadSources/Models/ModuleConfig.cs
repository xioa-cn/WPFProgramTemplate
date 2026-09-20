using System.Xml.Serialization;

namespace ModuleLoadSources.Models;

[XmlRoot("wpfModules")]
public class ModuleConfig : ModuleBase
{
    [XmlElement("unitModules")]
    public List<UnitModule> UnitModules { get; set; } = [];
}

public class UnitModule : ModuleBase
{
    [XmlElement("dllModule")]
    public List<DllModule> DllModules { get; set; } = [];
    [XmlAttribute("LoadMode")]
    public ModuleLoadMode LoadMode { get; set; }
}

public class DllModule : ModuleBase
{
    [XmlElement("loadClass")]
    public List<LoadClass> LoadClasses { get; set; } = [];
    [XmlAttribute("Dll")]
    public string Dll { get; set; } = string.Empty;
    [XmlAttribute("Path")]
    public string Path { get; set; } = string.Empty;
}

public class LoadClass : ModuleBase
{
}

public class ModuleBase
{
    [XmlAttribute("Name")]
    public string Name { get; set; } = string.Empty;
}
