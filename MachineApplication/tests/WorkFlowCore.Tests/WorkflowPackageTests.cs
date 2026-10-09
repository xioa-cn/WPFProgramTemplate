using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CSharpScriptCore.Models;
using CsxPad.Wpf.Services;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Script;
using WorkFlowCore.Services;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class WorkflowPackageTests
{
    [Fact]
    public void BackgroundReadDoesNotTouchEditorAndAppliesPreparedScriptsOnOwnerThread()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            var original = new ScriptClassNode { PackageWorkspaceDirectory = Path.Combine(root, "packages") };
            editor.Nodes.Add(original);
            var path = Path.Combine(root, "async.workflow.json");
            WorkflowPackageService.Save(editor, path);
            File.WriteAllText(original.ScriptFilePath!, "public class WorkflowScript { public static int Execute() => 73; }");
            var prepared = WorkflowPackageService.ReadAsync(path).GetAwaiter().GetResult();
            Assert.Same(original, Assert.Single(editor.Nodes.OfType<ScriptClassNode>()));
            File.Delete(path);
            File.Delete(original.ScriptFilePath!);
            prepared.ApplyTo(editor);
            var loaded = Assert.Single(editor.Nodes.OfType<ScriptClassNode>());
            Assert.NotSame(original, loaded);
            Assert.Equal(original.Guid, loaded.Guid);
            Assert.Contains("=> 73", loaded.ScriptCode);
            Assert.Equal(original.PackageWorkspaceDirectory, loaded.PackageWorkspaceDirectory);
        });
    }

    [Fact]
    public void PreparedLoadPreservesCanvasWhenNodeTypeValidationFails()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            var original = new ScriptClassNode();
            editor.Nodes.Add(original);
            var canvas = JsonNode.Parse(editor.GetCanvasData())!;
            canvas["Nodes"]![0]!["Type"] = "Unknown:Node";
            var path = Path.Combine(root, "invalid.workflow.json");
            File.WriteAllText(path, canvas.ToJsonString());
            var prepared = WorkflowPackageService.ReadAsync(path).GetAwaiter().GetResult();
            Assert.Throws<InvalidDataException>(() => prepared.ApplyTo(editor));
            Assert.Same(original, Assert.Single(editor.Nodes.OfType<ScriptClassNode>()));
        });
    }

    [Fact]
    public void DefaultSavePathIsUnderExecutableWorkflowDirectory()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "workflow"), WorkflowPackageService.DefaultDirectory);
        Assert.Equal(WorkflowPackageService.DefaultDirectory, Path.GetDirectoryName(WorkflowPackageService.CreateDefaultFilePath()));
    }

    [Fact]
    public void SaveExportsScriptsAndPreservesConnectionsAndExternalScriptEdits()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            var definition = new ScriptClassNode();
            var method = new ScriptMethodNode();
            editor.Nodes.Add(definition);
            editor.Nodes.Add(method);
            foreach (var node in editor.Nodes.OfType<ScriptNode>())
                node.PackageWorkspaceDirectory = Path.Combine(root, "empty-packages");
            Assert.Equal(ConnectionStatus.Connected, definition.Output.ConnectOption(method.GetInputOptions().First()));
            var path = Path.Combine(root, "flow.workflow.json");
            WorkflowPackageService.Save(editor, path);
            Assert.True(File.Exists(definition.ScriptFilePath));
            File.WriteAllText(definition.ScriptFilePath!, "public class WorkflowScript { public static int Execute() => 17; }");
            var restored = CreateEditor();
            WorkflowPackageService.Load(restored, path);
            var loaded = Assert.Single(restored.Nodes.OfType<ScriptClassNode>());
            Assert.Contains("=> 17", loaded.ScriptCode);
            Assert.Single(loaded.Output.ConnectedOption);
            Assert.Equal(definition.Guid, loaded.Guid);
            Assert.Equal(Path.GetDirectoryName(loaded.ScriptFilePath), loaded.ScriptDirectory);
        });
    }

    [Fact]
    public void SaveAsCopiesLocalReferencesAndLoadsWithoutOriginalDirectory()
    {
        InWorkspace(root =>
        {
            var original = Path.Combine(root, "original");
            Directory.CreateDirectory(original);
            File.Copy(typeof(ScriptExecutionOptions).Assembly.Location, Path.Combine(original, "CSharpScriptCore.dll"));
            File.WriteAllText(Path.Combine(original, "helper.csx"), "public class Helper { public static int Value => 41; }");
            var editor = CreateEditor();
            var node = new ScriptMethodNode
            {
                ScriptDirectory = original,
                PackageWorkspaceDirectory = Path.Combine(original, "nuget"),
                ScriptCode = "#r \"CSharpScriptCore.dll\"\n#load \"helper.csx\"\npublic class WorkflowScript { public static int Execute(int value = 0) => Helper.Value + 1; }"
            };
            editor.Nodes.Add(node);
            var first = Path.Combine(original, "first.workflow.json");
            WorkflowPackageService.Save(editor, first);
            var firstScript = node.ScriptFilePath!;
            var second = Path.Combine(root, "copy", "second.workflow.json");
            WorkflowPackageService.Save(editor, second);
            Assert.NotEqual(firstScript, node.ScriptFilePath);
            Assert.True(File.Exists(firstScript));
            Directory.Delete(original, true);
            var restored = CreateEditor();
            WorkflowPackageService.Load(restored, second);
            var loaded = Assert.Single(restored.Nodes.OfType<ScriptMethodNode>());
            var paths = new NuGetWorkspaceService(loaded.PackageWorkspaceDirectory)
                .GetScriptReferencePathsAsync(loaded.ScriptCode, CancellationToken.None, loaded.ScriptFilePath)
                .GetAwaiter().GetResult();
            Assert.Contains(paths, reference => reference.EndsWith("CSharpScriptCore.dll", StringComparison.OrdinalIgnoreCase));
            var result = loaded.Execute(new EditorExecutionContext());
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(42, loaded.Output.Data);
        });
    }

    [Fact]
    public void RelocatedPackageRecognizesInstalledNuGetDllsWithoutRestore()
    {
        InWorkspace(root =>
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var workspace = CreateInstalledWorkspace(Path.Combine(source, "nuget"));
            var editor = CreateEditor();
            editor.Nodes.Add(new ScriptClassNode { PackageWorkspaceDirectory = workspace });
            var file = Path.Combine(source, "saved", "flow.workflow.json");
            WorkflowPackageService.Save(editor, file);
            var moved = Path.Combine(root, "moved");
            Directory.Move(Path.GetDirectoryName(file)!, moved);
            Directory.Delete(source, true);
            var restored = CreateEditor();
            WorkflowPackageService.Load(restored, Path.Combine(moved, Path.GetFileName(file)));
            var node = Assert.Single(restored.Nodes.OfType<ScriptClassNode>());
            var nuget = new NuGetWorkspaceService(node.PackageWorkspaceDirectory);
            Assert.Equal("Fixture.Package", Assert.Single(nuget.GetInstalledPackages()).Id);
            var references = nuget.GetCompileReferencePathsAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.StartsWith(moved, Assert.Single(references), StringComparison.OrdinalIgnoreCase);
            var options = new ScriptClassDefinition("",  "", node.ScriptDirectory)
            {
                PackageWorkspaceDirectory = node.PackageWorkspaceDirectory,
                ScriptPath = node.ScriptFilePath
            }.CreateOptions();
            options.IncludeLoadedAssemblies = false;
            var execution = Task.Run(() => CSharpScriptCore.CSharpScript.ExecuteCodeAsync(
                "#r \"nuget: Fixture.Package\"\nnew CSharpScriptCore.Models.ScriptExecutionOptions().EnableNuGetDirectives", options)).GetAwaiter().GetResult();
            Assert.True(execution.Success, execution.Output);
            Assert.Equal(true, execution.ReturnValue);
        });
    }

    [Fact]
    public void FailedSavePreservesExistingDocumentAndNodePaths()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            var node = new ScriptClassNode { PackageWorkspaceDirectory = Path.Combine(root, "empty") };
            editor.Nodes.Add(node);
            var file = Path.Combine(root, "flow.workflow.json");
            WorkflowPackageService.Save(editor, file);
            var original = File.ReadAllBytes(file);
            var originalScript = node.ScriptFilePath;
            node.ScriptCode = "#load \"missing.csx\"\n" + node.ScriptCode;
            Assert.Throws<FileNotFoundException>(() => WorkflowPackageService.Save(editor, file));
            Assert.Equal(original, File.ReadAllBytes(file));
            Assert.Equal(originalScript, node.ScriptFilePath);
            Assert.Contains("missing.csx", node.ScriptCode);
        });
    }

    [Fact]
    public void InvalidPackagePathsAndMissingScriptsDoNotReplaceCurrentCanvas()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            var node = new ScriptClassNode { PackageWorkspaceDirectory = Path.Combine(root, "empty") };
            editor.Nodes.Add(node);
            var file = Path.Combine(root, "flow.workflow.json");
            WorkflowPackageService.Save(editor, file);
            File.Delete(node.ScriptFilePath!);
            Assert.Throws<FileNotFoundException>(() => WorkflowPackageService.Load(editor, file));
            Assert.Same(node, Assert.Single(editor.Nodes));
            var json = JsonNode.Parse(File.ReadAllText(file))!;
            json["ScriptPackage"]!["Directory"] = "../outside";
            File.WriteAllText(file, json.ToJsonString());
            Assert.Throws<InvalidDataException>(() => WorkflowPackageService.Load(editor, file));
            Assert.Same(node, Assert.Single(editor.Nodes));
        });
    }

    [Fact]
    public void LegacyCanvasStillLoads()
    {
        InWorkspace(root =>
        {
            var editor = CreateEditor();
            editor.Nodes.Add(new ScriptClassNode());
            var file = Path.Combine(root, "legacy.workflow.json");
            editor.SaveCanvas(file);
            WorkflowPackageService.Load(editor, file);
            Assert.Null(Assert.Single(editor.Nodes.OfType<ScriptClassNode>()).PackageWorkspaceDirectory);
        });
    }

    private static XTNodeEditor CreateEditor()
    {
        var editor = new XTNodeEditor();
        editor.RegisterNodeType(typeof(ScriptClassNode));
        editor.RegisterNodeType(typeof(ScriptMethodNode));
        return editor;
    }

    private static string CreateInstalledWorkspace(string workspace)
    {
        var package = Path.Combine(workspace, "packages", "fixture.package", "1.0.0");
        Directory.CreateDirectory(Path.Combine(package, "lib", "net8.0"));
        File.Copy(typeof(ScriptExecutionOptions).Assembly.Location, Path.Combine(package, "lib", "net8.0", "CSharpScriptCore.dll"));
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup", new XElement("TargetFramework", "net8.0"),
                new XElement("RuntimeIdentifier", RuntimeInformation.RuntimeIdentifier),
                new XElement("RestorePackagesPath", Path.Combine(workspace, "packages"))),
            new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Fixture.Package"),
                new XAttribute("Version", "1.0.0"))))).Save(Path.Combine(workspace, "ScriptPackages.csproj"));
        Directory.CreateDirectory(Path.Combine(workspace, "obj"));
        var assets = new
        {
            targets = new Dictionary<string, object>
            {
                ["net8.0"] = new Dictionary<string, object>
                {
                    ["Fixture.Package/1.0.0"] = new { compile = new Dictionary<string, object>
                    { ["lib/net8.0/CSharpScriptCore.dll"] = new { } } }
                }
            },
            libraries = new Dictionary<string, object>
            {
                ["Fixture.Package/1.0.0"] = new { type = "package", path = "fixture.package/1.0.0" }
            },
            packageFolders = new Dictionary<string, object> { [Path.Combine(workspace, "packages")] = new { } }
        };
        File.WriteAllText(Path.Combine(workspace, "obj", "project.assets.json"), JsonSerializer.Serialize(assets));
        return workspace;
    }

    private static void InWorkspace(Action<string> action)
    {
        Exception? failure = null;
        var root = Path.Combine(Path.GetTempPath(), "WorkflowPackageTests", Guid.NewGuid().ToString("N"));
        var thread = new Thread(() =>
        {
            try { Directory.CreateDirectory(root); action(root); }
            catch (Exception exception) { failure = exception; }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "工作流包测试超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
