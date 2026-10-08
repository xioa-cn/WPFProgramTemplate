using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Script;

namespace WorkFlowCore.Services;

public static class WorkflowPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "workflow");
    public static string DefaultFilePath => Path.Combine(DefaultDirectory, "workflow.workflow.json");

    public static string CreateDefaultFilePath()
    {
        var path = DefaultFilePath;
        for (var suffix = 2; File.Exists(path); suffix++)
            path = Path.Combine(DefaultDirectory, $"workflow-{suffix}.workflow.json");
        return path;
    }

    public static void Save(XTNodeEditor editor, string filePath)
    {
        var target = Path.GetFullPath(filePath);
        var parent = Path.GetDirectoryName(target)!;
        var canvas = JsonNode.Parse(editor.GetCanvasData())!.AsObject();
        var package = new PackageManifest
        {
            Directory = Path.GetFileName(target) + ".assets/" + Guid.NewGuid().ToString("N")
        };
        var root = ResolveInside(parent, package.Directory);
        var codes = new Dictionary<Guid, string>();
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(root);
        try
        {
            var workspaces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var exporter = new ScriptExporter(root);
            var documents = canvas["Nodes"]!.AsArray().ToDictionary(item => item!["Id"]!.GetValue<Guid>());
            foreach (var node in editor.Nodes.OfType<ScriptNode>())
            {
                var sourceDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(node.ScriptDirectory)
                    ? Environment.CurrentDirectory : node.ScriptDirectory);
                var script = $"scripts/{node.Guid:N}.csx";
                var code = exporter.Export(node.ScriptCode, sourceDirectory, script);
                codes.Add(node.Guid, code);
                var workspaceSource = Path.GetFullPath(node.PackageWorkspaceDirectory ??
                    Path.Combine(AppContext.BaseDirectory, "Data", "NuGet"));
                if (!workspaces.TryGetValue(workspaceSource, out var workspace))
                {
                    workspace = $"dependencies/nuget-{workspaces.Count + 1}";
                    CopyWorkspace(workspaceSource, ResolveInside(root, workspace));
                    workspaces.Add(workspaceSource, workspace);
                }
                package.Scripts.Add(node.Guid, new ScriptEntry(script, workspace));
                var properties = documents[node.Guid]!["Properties"]!.AsObject();
                properties[nameof(ScriptNode.ScriptCode)] = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
                properties[nameof(ScriptNode.ScriptDirectory)] = Convert.ToBase64String(Encoding.UTF8.GetBytes("scripts"));
            }
            canvas["ScriptPackage"] = JsonSerializer.SerializeToNode(package);
            var data = Encoding.UTF8.GetBytes(canvas.ToJsonString(JsonOptions));
            if (data.Length > 32 * 1024 * 1024) throw new InvalidDataException("工作流文件超过 32MB 限制。");
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, target, true);
        }
        catch
        {
            TryDeleteGeneration(parent, package.Directory, target);
            throw;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        Attach(editor, root, package, codes);
    }

    public static void Load(XTNodeEditor editor, string filePath)
    {
        var target = Path.GetFullPath(filePath);
        if (new FileInfo(target).Length > 32 * 1024 * 1024)
            throw new InvalidDataException("工作流文件超过 32MB 限制。");
        var data = File.ReadAllBytes(target);
        var canvas = JsonNode.Parse(data)!.AsObject();
        var package = canvas["ScriptPackage"]?.Deserialize<PackageManifest>();
        if (package is null)
        {
            editor.LoadCanvas(data);
            return;
        }
        if (package.Version != 1) throw new InvalidDataException("不支持的工作流资源版本。");
        var root = ResolveInside(Path.GetDirectoryName(target)!, package.Directory);
        var codes = new Dictionary<Guid, string>();
        var documents = canvas["Nodes"]!.AsArray().ToDictionary(item => item!["Id"]!.GetValue<Guid>());
        foreach (var (nodeId, entry) in package.Scripts)
        {
            var script = ResolveInside(root, entry.Script);
            var workspace = ResolveInside(root, entry.Workspace);
            if (!documents.TryGetValue(nodeId, out var document))
                throw new InvalidDataException("脚本资源对应的节点不存在。");
            if (!Directory.Exists(workspace)) throw new DirectoryNotFoundException($"缺少脚本依赖目录：{workspace}");
            var code = File.ReadAllText(script);
            codes.Add(nodeId, code);
            var properties = document!["Properties"]!.AsObject();
            properties[nameof(ScriptNode.ScriptCode)] = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
            properties[nameof(ScriptNode.ScriptDirectory)] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetDirectoryName(script)!));
        }
        editor.LoadCanvas(Encoding.UTF8.GetBytes(canvas.ToJsonString()));
        Attach(editor, root, package, codes);
    }

    private static void Attach(XTNodeEditor editor, string root, PackageManifest package, IReadOnlyDictionary<Guid, string> codes)
    {
        foreach (var node in editor.Nodes.OfType<ScriptNode>())
        {
            if (!package.Scripts.TryGetValue(node.Guid, out var entry)) continue;
            node.ScriptFilePath = ResolveInside(root, entry.Script);
            node.ScriptDirectory = Path.GetDirectoryName(node.ScriptFilePath)!;
            node.PackageWorkspaceDirectory = ResolveInside(root, entry.Workspace);
            node.ScriptCode = codes[node.Guid];
        }
    }

    private static void CopyWorkspace(string source, string target)
    {
        Directory.CreateDirectory(target);
        var projectPath = Path.Combine(source, "ScriptPackages.csproj");
        if (!File.Exists(projectPath)) return;
        var project = XDocument.Load(projectPath);
        var assetsPath = Path.Combine(source, "obj", "project.assets.json");
        if (project.Descendants("PackageReference").Any() && !File.Exists(assetsPath))
            throw new InvalidDataException("脚本 NuGet 依赖尚未还原，请先在 CsxPad 完成安装后保存。");
        project.Descendants("RestorePackagesPath").FirstOrDefault()?.SetValue(Path.Combine(target, "packages"));
        project.Save(Path.Combine(target, "ScriptPackages.csproj"));
        if (!File.Exists(assetsPath)) return;
        var assets = JsonNode.Parse(File.ReadAllText(assetsPath))!.AsObject();
        var folders = new[] { Path.Combine(source, "packages") }
            .Concat(assets["packageFolders"]!.AsObject().Select(item => item.Key)).ToArray();
        foreach (var library in assets["libraries"]!.AsObject())
        {
            if (library.Value?["type"]?.GetValue<string>() != "package") continue;
            var relativePath = library.Value["path"]!.GetValue<string>();
            var packageSource = folders.Select(folder => ResolveInside(folder, relativePath)).FirstOrDefault(Directory.Exists)
                ?? throw new DirectoryNotFoundException($"找不到已安装的脚本包：{library.Key}");
            CopyDirectory(packageSource, ResolveInside(Path.Combine(target, "packages"), relativePath));
        }
        assets["packageFolders"] = new JsonObject { [Path.Combine(target, "packages") + Path.DirectorySeparatorChar] = new JsonObject() };
        Directory.CreateDirectory(Path.Combine(target, "obj"));
        File.WriteAllText(Path.Combine(target, "obj", "project.assets.json"), assets.ToJsonString(JsonOptions));
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"不能打包符号链接：{file}");
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"不能打包符号链接：{directory}");
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string ResolveInside(string root, string relativePath)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(prefix, relativePath));
        if (Path.IsPathRooted(relativePath) || !fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("工作流资源路径必须位于资源目录内。");
        return fullPath;
    }

    private static void TryDeleteGeneration(string parent, string relativePath, string target)
    {
        var fullPath = ResolveInside(parent, relativePath);
        var ownedRoot = target + ".assets" + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(ownedRoot, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(fullPath), "N", out _)) return;
        try { if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public sealed class PackageManifest
    {
        public int Version { get; set; } = 1;
        public string Directory { get; set; } = string.Empty;
        public Dictionary<Guid, ScriptEntry> Scripts { get; set; } = [];
    }

    public sealed record ScriptEntry(string Script, string Workspace);

    private sealed class ScriptExporter(string root)
    {
        private readonly Dictionary<string, string> _loads = new(StringComparer.OrdinalIgnoreCase);

        public string Export(string code, string sourceDirectory, string destination)
        {
            var syntax = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script)).GetRoot();
            var directives = syntax.DescendantTrivia().Select(trivia => trivia.GetStructure())
                .OfType<DirectiveTriviaSyntax>().Where(directive => directive.IsActive).Reverse();
            var output = ResolveInside(root, destination);
            foreach (var directive in directives)
            {
                var token = directive switch
                {
                    LoadDirectiveTriviaSyntax load => load.File,
                    ReferenceDirectiveTriviaSyntax reference => reference.File,
                    _ => default
                };
                if (token.RawKind == 0) continue;
                var value = token.ValueText;
                if (value.StartsWith("nuget:", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("framework:", StringComparison.OrdinalIgnoreCase)) continue;
                var source = Path.GetFullPath(Path.Combine(sourceDirectory, value));
                var isLoad = directive is LoadDirectiveTriviaSyntax;
                if (!File.Exists(source))
                {
                    if (!isLoad && !value.Contains('/') && !value.Contains('\\') &&
                        !value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                        !value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    throw new FileNotFoundException("无法保存脚本引用，请检查脚本目录。", source);
                }
                string relative;
                if (isLoad)
                {
                    if (!_loads.TryGetValue(source, out relative!))
                    {
                        relative = "scripts/loads/" + Hash(source) + ".csx";
                        _loads.Add(source, relative);
                        Export(File.ReadAllText(source), Path.GetDirectoryName(source)!, relative);
                    }
                }
                else
                {
                    var directory = Path.GetDirectoryName(source)!;
                    relative = "dependencies/dll/" + Hash(directory) + "/" + Path.GetFileName(source);
                    var targetDirectory = Path.GetDirectoryName(ResolveInside(root, relative))!;
                    Directory.CreateDirectory(targetDirectory);
                    foreach (var dependency in Directory.EnumerateFiles(directory, "*.dll"))
                        File.Copy(dependency, Path.Combine(targetDirectory, Path.GetFileName(dependency)), true);
                    File.Copy(source, ResolveInside(root, relative), true);
                }
                var replacement = Path.GetRelativePath(Path.GetDirectoryName(output)!, ResolveInside(root, relative)).Replace('\\', '/');
                code = code.Remove(token.SpanStart, token.Span.Length).Insert(token.SpanStart, "\"" + replacement + "\"");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, code, new UTF8Encoding(false));
            return code;
        }

        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..20];
    }
}
