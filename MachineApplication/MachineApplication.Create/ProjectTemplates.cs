using System.Xml.Linq;
using System.Text.Json;

namespace MachineApplication.Create;

internal static class ProjectTemplates
{
    public static Dictionary<string, string> Create(string root, string target, string project, string module, bool createModule, bool loadDesignerStyles)
    {
        var basePath = Path.Combine(root, "src", "Machine.ModuleLoad", "Machine.ModuleLoad.csproj");
        var analyzer = Path.Combine(root, "src", "I18n.LangsGenerator", "I18n.LangsGenerator.csproj");
        if (!File.Exists(basePath) || !File.Exists(analyzer))
            throw new FileNotFoundException("解决方案中缺少 Machine.ModuleLoad 或 I18n.LangsGenerator 项目。");
        var baseProject = XDocument.Load(basePath);
        var sdk = (string?)baseProject.Root?.Attribute("Sdk") ?? throw new InvalidDataException("基类项目缺少 SDK。");
        var framework = baseProject.Descendants("TargetFramework").First().Value;
        var xml = new XElement("Project", new XAttribute("Sdk", sdk),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", framework),
                new XElement("UseWPF", "true"),
                new XElement("Nullable", "enable"),
                new XElement("ImplicitUsings", "enable")),
            new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(target, basePath))),
                new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(target, analyzer)),
                    new XAttribute("OutputItemType", "Analyzer"), new XAttribute("ReferenceOutputAssembly", "false")),
                new XElement("AdditionalFiles", new XAttribute("Include", "**/lang.*.json"),
                    new XAttribute("Exclude", "bin/**;obj/**"))));
        if (loadDesignerStyles)
            DesignResourcesTemplate.Configure(xml, root, target);
        var files = new Dictionary<string, string>
        {
            [project + ".csproj"] = xml.ToString()
        };
        if (!createModule) return files;
        var languageFiles = new Dictionary<string, string>
        {
            ["Resources/lang.zh.json"] = JsonSerializer.Serialize(new { Global = new { ModuleName = project } }, new JsonSerializerOptions { WriteIndented = true }),
            [$"Resources/{module}Lang.cs"] = $$"""
                using I18nExtensions;

                namespace {{project}}.Resources;

                public partial class {{module}}Lang : LangBase
                {
                }
                """
        };
        foreach (var (name, content) in languageFiles) files[name] = content;
        if (createModule)
            files[module + ".cs"] = $$"""
                using Machine.ModuleLoad;
                using Machine.ModuleLoad.ModuleConfig;
                using Microsoft.Extensions.DependencyInjection;

                namespace {{project}};

                public sealed class {{module}} : IModule
                {
                    public void ModuleStartupCommand(StartupCommand? command = null)
                    {
                    }

                    public void RegisterTypes(IServiceCollection servicesCollection)
                    {
                    }

                    public void OnInitialized(IServiceProvider containerProvider)
                    {
                    }

                    public void OnShutdown()
                    {
                    }
                }
                """;
        return files;
    }
}






