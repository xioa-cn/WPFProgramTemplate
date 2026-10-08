using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CSharpScriptCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpScriptCore.Core;

internal sealed partial class ScriptDependencyResolver
{
    internal const string AspNetCoreFrameworkName = "Microsoft.AspNetCore.App";
    internal const string ConsoleFrameworkName = "Console";
    private const string NuGetSearchEndpoint = "https://azuresearch-usnc.nuget.org/query";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WorkspaceLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<IReadOnlyList<string>> AspNetCoreReferences = new(FindAspNetCoreReferences);

    private readonly ScriptExecutionOptions _options;
    private readonly string _workspaceDirectory;
    private readonly string _packageDirectory;
    private readonly string _projectPath;
    private readonly string _assetsPath;

    private string EffectiveRuntimeIdentifier =>
        !string.IsNullOrWhiteSpace(_options.RuntimeIdentifier)
            ? _options.RuntimeIdentifier
            : RuntimeInformation.RuntimeIdentifier;

    public ScriptDependencyResolver(ScriptExecutionOptions options)
    {
        _options = options;
        _workspaceDirectory = Path.GetFullPath(options.PackageWorkspaceDirectory);
        _packageDirectory = Path.Combine(_workspaceDirectory, "packages");
        _projectPath = Path.Combine(_workspaceDirectory, "ScriptPackages.csproj");
        _assetsPath = Path.Combine(_workspaceDirectory, "obj", "project.assets.json");
    }

    public static bool UsesFramework(string code, string frameworkName) =>
        FrameworkDirectiveRegex().Matches(code).Cast<Match>().Any(match => string.Equals(
            match.Groups[1].Value.Trim(), frameworkName, StringComparison.OrdinalIgnoreCase));

    public async Task<PreparedScript> PrepareAsync(string code, CancellationToken cancellationToken)
    {
        var expandedCode = ExpandLoadGraph(code);
        var restoreOutput = new List<string>();
        var gate = WorkspaceLocks.GetOrAdd(_workspaceDirectory, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureProject();
            if (_options.EnableNuGetDirectives)
            {
                var installed = ReadInstalledPackages();
                foreach (Match match in NuGetDirectiveRegex().Matches(expandedCode))
                {
                    var id = match.Groups[1].Value.Trim();
                    var version = match.Groups[2].Value.Trim();
                    if (version.Length == 0)
                    {
                        version = installed.TryGetValue(id, out var installedVersion) && !string.IsNullOrWhiteSpace(installedVersion)
                            ? installedVersion
                            : await FindLatestVersionAsync(id, cancellationToken).ConfigureAwait(false);
                    }

                    if (!installed.TryGetValue(id, out var currentVersion) || currentVersion != version)
                    {
                        UpdatePackage(id, version);
                        installed[id] = version;
                        restoreOutput.Add($"Restored {id} {version}");
                    }
                }
            }

            if ((!File.Exists(_assetsPath) && ReadInstalledPackages().Count > 0) || restoreOutput.Count > 0 ||
                ProjectSettingsChanged())
            {
                EnsureProject(forceSettingsUpdate: true);
                var output = await RestoreAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(output)) restoreOutput.Add(output);
            }

            var references = ReadManagedAssets()
                .Concat(GetFrameworkReferences(expandedCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new PreparedScript(
                RemoveHostDirectives(expandedCode),
                references,
                string.Join(Environment.NewLine, restoreOutput));
        }
        finally
        {
            gate.Release();
        }
    }

    private string ExpandLoadGraph(string rootCode)
    {
        var baseDirectory = !string.IsNullOrWhiteSpace(_options.ScriptPath)
            ? Path.GetDirectoryName(Path.GetFullPath(_options.ScriptPath)) ?? _options.BaseDirectory
            : Path.GetFullPath(_options.BaseDirectory);
        var activePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_options.ScriptPath))
        {
            activePaths.Add(Path.GetFullPath(_options.ScriptPath));
        }
        var usingDirectives = new List<string>();
        var expandedCode = ExpandLoads(
            rootCode,
            baseDirectory,
            activePaths,
            _options.ScriptPath,
            usingDirectives);
        var header = string.Join(
            Environment.NewLine,
            usingDirectives.Distinct(StringComparer.Ordinal));
        return header.Length == 0
            ? expandedCode
            : $"#line hidden{Environment.NewLine}{header}{Environment.NewLine}" +
              $"#line 1{Environment.NewLine}{expandedCode}";
    }

    private static string ExpandLoads(
        string code,
        string baseDirectory,
        HashSet<string> activePaths,
        string? parentPath,
        ICollection<string> usingDirectives)
    {
        code = ExtractTopLevelUsingDirectives(code, usingDirectives);
        return LoadDirectiveRegex().Replace(code, match =>
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, match.Groups["path"].Value));
            if (!File.Exists(fullPath))
            {
                throw new ScriptDependencyException($"Loaded script '{fullPath}' was not found.");
            }
            if (!activePaths.Add(fullPath))
            {
                throw new ScriptDependencyException($"Circular #load detected at '{fullPath}'.");
            }

            try
            {
                var loadedCode = File.ReadAllText(fullPath);
                var expanded = ExpandLoads(
                    loadedCode,
                    Path.GetDirectoryName(fullPath) ?? baseDirectory,
                    activePaths,
                    fullPath,
                    usingDirectives);
                var escapedPath = fullPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
                var resetPath = string.IsNullOrWhiteSpace(parentPath)
                    ? string.Empty
                    : $"#line default{Environment.NewLine}";
                return $"#line 1 \"{escapedPath}\"{Environment.NewLine}{expanded}{Environment.NewLine}{resetPath}";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ScriptDependencyException($"Unable to read loaded script '{fullPath}'.", exception);
            }
            finally
            {
                activePaths.Remove(fullPath);
            }
        });
    }

    private static string ExtractTopLevelUsingDirectives(
        string code,
        ICollection<string> usingDirectives)
    {
        var root = CSharpSyntaxTree.ParseText(
                code,
                new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script))
            .GetCompilationUnitRoot();
        var topLevelUsings = root.DescendantNodes(descendIntoTrivia: false)
            .OfType<UsingDirectiveSyntax>()
            .Where(item => item.Parent is CompilationUnitSyntax)
            .OrderBy(item => item.SpanStart)
            .ToArray();
        if (topLevelUsings.Length == 0) return code;

        var characters = code.ToCharArray();
        foreach (var usingDirective in topLevelUsings)
        {
            usingDirectives.Add(usingDirective.WithoutTrivia().ToFullString().Trim());
            for (var index = usingDirective.SpanStart; index < usingDirective.Span.End; index++)
            {
                if (characters[index] is not '\r' and not '\n') characters[index] = ' ';
            }
        }
        return new string(characters);
    }

    private Dictionary<string, string> ReadInstalledPackages()
    {
        var project = XDocument.Load(_projectPath);
        return project.Descendants("PackageReference")
            .Where(item => item.Attribute("Include") is not null)
            .ToDictionary(
                item => item.Attribute("Include")!.Value,
                item => item.Attribute("Version")?.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureProject(bool forceSettingsUpdate = false)
    {
        Directory.CreateDirectory(_workspaceDirectory);
        if (!File.Exists(_projectPath))
        {
            CreateProject().Save(_projectPath);
            return;
        }
        if (!forceSettingsUpdate) return;

        var project = XDocument.Load(_projectPath);
        var propertyGroup = project.Root!.Elements("PropertyGroup").FirstOrDefault()
                            ?? new XElement("PropertyGroup");
        if (propertyGroup.Parent is null) project.Root.AddFirst(propertyGroup);
        SetProperty(propertyGroup, "TargetFramework", "net8.0");
        SetProperty(propertyGroup, "RestorePackagesPath", _packageDirectory);
        SetProperty(propertyGroup, "RuntimeIdentifier", EffectiveRuntimeIdentifier);
        project.Save(_projectPath);
    }

    private XDocument CreateProject() => new(
        new XElement("Project",
            new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net8.0"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("RestorePackagesPath", _packageDirectory),
                new XElement("RuntimeIdentifier", EffectiveRuntimeIdentifier)),
            new XElement("ItemGroup")));

    private bool ProjectSettingsChanged()
    {
        if (!File.Exists(_projectPath)) return true;
        var project = XDocument.Load(_projectPath);
        var rid = project.Descendants("RuntimeIdentifier").FirstOrDefault()?.Value;
        return !string.Equals(rid ?? string.Empty, EffectiveRuntimeIdentifier,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void SetProperty(XElement group, string name, string? value)
    {
        var element = group.Element(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            element?.Remove();
            return;
        }
        if (element is null) group.Add(new XElement(name, value));
        else element.Value = value;
    }

    private void UpdatePackage(string id, string version)
    {
        var project = XDocument.Load(_projectPath);
        var itemGroup = project.Root!.Elements("ItemGroup").FirstOrDefault() ?? new XElement("ItemGroup");
        if (itemGroup.Parent is null) project.Root.Add(itemGroup);
        var reference = itemGroup.Elements("PackageReference").FirstOrDefault(item => string.Equals(
            item.Attribute("Include")?.Value, id, StringComparison.OrdinalIgnoreCase));
        if (reference is null)
        {
            itemGroup.Add(new XElement("PackageReference",
                new XAttribute("Include", id), new XAttribute("Version", version)));
        }
        else
        {
            reference.SetAttributeValue("Version", version);
        }
        project.Save(_projectPath);
    }

    private async Task<string> FindLatestVersionAsync(string id, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var url = $"{NuGetSearchEndpoint}?q=packageid:{Uri.EscapeDataString(id)}&prerelease=false&take=20";
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
        {
            if (string.Equals(item.GetProperty("id").GetString(), id, StringComparison.OrdinalIgnoreCase))
            {
                return item.GetProperty("version").GetString()
                       ?? throw new InvalidOperationException($"NuGet package '{id}' has no version.");
            }
        }
        throw new InvalidOperationException($"NuGet package '{id}' was not found.");
    }

    private async Task<string> RestoreAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _workspaceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add(_projectPath);
        startInfo.ArgumentList.Add("--nologo");
        if (!string.IsNullOrWhiteSpace(_options.NuGetConfigFile))
        {
            startInfo.ArgumentList.Add("--configfile");
            startInfo.ArgumentList.Add(Path.GetFullPath(_options.NuGetConfigFile));
        }
        foreach (var source in _options.NuGetSources)
        {
            startInfo.ArgumentList.Add("--source");
            startInfo.ArgumentList.Add(source);
        }
        startInfo.ArgumentList.Add("--runtime");
        startInfo.ArgumentList.Add(EffectiveRuntimeIdentifier);
        startInfo.Environment["NUGET_PACKAGES"] = _packageDirectory;
        startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(_workspaceDirectory, ".dotnet");
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Unable to start dotnet restore.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"NuGet restore failed.{Environment.NewLine}{error}{Environment.NewLine}{output}");
        }
        return string.Join(Environment.NewLine, new[] { output, error }
            .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
    }

    private IReadOnlyList<string> ReadManagedAssets()
    {
        if (!File.Exists(_assetsPath)) return [];
        using var stream = File.OpenRead(_assetsPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var packageFolders = new[] { _packageDirectory }
            .Concat(root.GetProperty("packageFolders").EnumerateObject().Select(item => item.Name)).ToArray();
        var libraries = root.GetProperty("libraries");
        var target = SelectTarget(root.GetProperty("targets"), EffectiveRuntimeIdentifier);
        if (target is null) return [];

        var compatibleRids = ReadCompatibleRuntimeIdentifiers(root, EffectiveRuntimeIdentifier);
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in target.Value.EnumerateObject())
        {
            if (!libraries.TryGetProperty(library.Name, out var metadata) ||
                !metadata.TryGetProperty("path", out var packagePathElement)) continue;
            var packagePath = packagePathElement.GetString();
            if (string.IsNullOrWhiteSpace(packagePath)) continue;

            AddAssets(paths, packageFolders, packagePath, library.Value, "compile");
            AddAssets(paths, packageFolders, packagePath, library.Value, "runtime");

            if (!library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets)) continue;
            var candidates = runtimeTargets.EnumerateObject()
                .Where(item => item.Value.TryGetProperty("assetType", out var assetType) &&
                               string.Equals(assetType.GetString(), "runtime", StringComparison.OrdinalIgnoreCase))
                .Select(item => new
                {
                    Asset = item,
                    Rid = item.Value.TryGetProperty("rid", out var rid) ? rid.GetString() : null
                })
                .Where(item => item.Rid is not null && compatibleRids.ContainsKey(item.Rid))
                .OrderByDescending(item => compatibleRids[item.Rid!]);

            foreach (var candidate in candidates)
            {
                AddAsset(paths, packageFolders, packagePath, candidate.Asset.Name);
            }
        }
        return paths.Values.ToArray();
    }

    private static JsonElement? SelectTarget(JsonElement targets, string runtimeIdentifier)
    {
        var allTargets = targets.EnumerateObject().ToArray();
        var runtimeTarget = allTargets.FirstOrDefault(item =>
            item.Name.EndsWith('/' + runtimeIdentifier, StringComparison.OrdinalIgnoreCase));
        if (runtimeTarget.Value.ValueKind != JsonValueKind.Undefined) return runtimeTarget.Value;

        var frameworkTarget = allTargets.FirstOrDefault(item => !item.Name.Contains('/'));
        return frameworkTarget.Value.ValueKind == JsonValueKind.Undefined ? null : frameworkTarget.Value;
    }

    private static Dictionary<string, int> ReadCompatibleRuntimeIdentifiers(
        JsonElement root,
        string runtimeIdentifier)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [runtimeIdentifier] = 0
        };
        if (!root.TryGetProperty("runtimes", out var runtimes)) return result;

        var pending = new Queue<string>();
        pending.Enqueue(runtimeIdentifier);
        while (pending.TryDequeue(out var current))
        {
            if (!runtimes.TryGetProperty(current, out var runtime)) continue;

            var nextDistance = result[current] + 1;
            foreach (var importedRid in EnumerateRuntimeImports(runtime))
            {
                if (string.IsNullOrWhiteSpace(importedRid) || result.ContainsKey(importedRid)) continue;
                result[importedRid] = nextDistance;
                pending.Enqueue(importedRid);
            }
        }
        return result;
    }

    private static IEnumerable<string> EnumerateRuntimeImports(JsonElement runtime)
    {
        var imports = runtime.ValueKind == JsonValueKind.Object &&
                      runtime.TryGetProperty("#import", out var importProperty)
            ? importProperty
            : runtime;
        if (imports.ValueKind != JsonValueKind.Array) yield break;

        foreach (var import in imports.EnumerateArray())
        {
            if (import.ValueKind == JsonValueKind.String && import.GetString() is { Length: > 0 } rid)
            {
                yield return rid;
            }
        }
    }

    private static void AddAssets(
        Dictionary<string, string> paths,
        IReadOnlyList<string> packageFolders,
        string packagePath,
        JsonElement library,
        string groupName)
    {
        if (!library.TryGetProperty(groupName, out var assets)) return;
        foreach (var asset in assets.EnumerateObject())
        {
            AddAsset(paths, packageFolders, packagePath, asset.Name);
        }
    }

    private static void AddAsset(
        Dictionary<string, string> paths,
        IReadOnlyList<string> packageFolders,
        string packagePath,
        string assetPath)
    {
        if (assetPath.EndsWith("_._", StringComparison.Ordinal)) return;
        foreach (var packageFolder in packageFolders)
        {
            var path = Path.GetFullPath(Path.Combine(packageFolder, packagePath, assetPath));
            if (!File.Exists(path) || !TryGetAssemblyName(path, out var assemblyName)) continue;
            paths[assemblyName] = path;
            return;
        }
    }

    private static bool TryGetAssemblyName(string path, out string assemblyName)
    {
        try
        {
            assemblyName = AssemblyName.GetAssemblyName(path).Name ?? Path.GetFileNameWithoutExtension(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            assemblyName = string.Empty;
            return false;
        }
        catch (FileLoadException)
        {
            assemblyName = string.Empty;
            return false;
        }
    }

    private static IEnumerable<string> GetFrameworkReferences(string code)
    {
        foreach (Match match in FrameworkDirectiveRegex().Matches(code))
        {
            var name = match.Groups[1].Value.Trim();
            if (string.Equals(name, ConsoleFrameworkName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(name, AspNetCoreFrameworkName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Framework '{name}' is not supported. Use '{AspNetCoreFrameworkName}' or '{ConsoleFrameworkName}'.");
            }
            foreach (var path in AspNetCoreReferences.Value) yield return path;
        }
    }

    private static IReadOnlyList<string> FindAspNetCoreReferences()
    {
        var directory = Path.GetDirectoryName(typeof(WebApplication).Assembly.Location);
        return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
            ? []
            : Directory.EnumerateFiles(directory, "*.dll").Where(IsManagedAssembly).ToArray();
    }

    private static bool IsManagedAssembly(string path)
    {
        try { _ = AssemblyName.GetAssemblyName(path); return true; }
        catch (BadImageFormatException) { return false; }
        catch (FileLoadException) { return false; }
    }

    private static string RemoveHostDirectives(string code)
    {
        var withoutNuGet = NuGetDirectiveRegex().Replace(code, PreserveLines);
        return FrameworkDirectiveRegex().Replace(withoutNuGet, PreserveLines);
    }

    private static string PreserveLines(Match match) =>
        new('\n', Math.Max(1, match.Value.Count(character => character == '\n')));

    [GeneratedRegex("^\\s*#r\\s+\"nuget:\\s*([^,\"]+)\\s*(?:,\\s*([^\"]+))?\"\\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex NuGetDirectiveRegex();

    [GeneratedRegex("^\\s*#r\\s+\"framework:\\s*([^\"]+)\"\\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex FrameworkDirectiveRegex();

    [GeneratedRegex("^[ \\t]*#load[ \\t]+\"(?<path>[^\"\\r\\n]+)\"[ \\t]*;?[ \\t]*(?=\\r?$)",
        RegexOptions.Multiline)]
    private static partial Regex LoadDirectiveRegex();
}

internal sealed class ScriptDependencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed record PreparedScript(
    string Code,
    IReadOnlyList<string> ReferencePaths,
    string RestoreOutput);
