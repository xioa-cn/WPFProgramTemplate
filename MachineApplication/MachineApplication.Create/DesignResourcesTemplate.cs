using System.Xml.Linq;

namespace MachineApplication.Create;

/// <summary>为模块链接设计器使用的共享样式，不生成应用入口。</summary>
internal static class DesignResourcesTemplate
{
    public static void Configure(XElement project, string root, string target)
    {
        var styles = Path.Combine(root, "src", "MachineApplication.Entrance", "AppResources.xaml");
        if (!File.Exists(styles))
            throw new FileNotFoundException("找不到公共样式 AppResources.xaml。", styles);
        project.Add(new XElement("ItemGroup",
            new XElement("Page", new XAttribute("Include", Path.GetRelativePath(target, styles)),
                new XElement("Link", "Design/SharedStyles.xaml"),
                new XElement("Generator", "MSBuild:Compile"))));
    }
}

