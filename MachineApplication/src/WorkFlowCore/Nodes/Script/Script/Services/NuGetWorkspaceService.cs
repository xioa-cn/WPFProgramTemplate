using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsxPad.Wpf.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsxPad.Wpf.Services;

public sealed partial class NuGetWorkspaceService
{
    private const string NuGetSearchEndpoint = "https://azuresearch-usnc.nuget.org/query";
    private const string AspNetCoreFrameworkName = "Microsoft.AspNetCore.App";
    private const string ConsoleFrameworkName = "Console";
    private static readonly Lazy<IReadOnlyList<string>> AspNetCoreReferencePaths =
        new(FindAspNetCoreReferencePaths);
    private readonly HttpClient _httpClient = new();
    private readonly string _workspaceDirectory;
    private readonly string _packageCacheDirectory;
    private readonly string _httpCacheDirectory;
    private readonly string _pluginCacheDirectory;
    private readonly string _dotnetHomeDirectory;
    private readonly string _projectPath;
    private readonly string _assetsPath;
    private bool _restoreRequired;
    private static string RuntimeIdentifier => RuntimeInformation.RuntimeIdentifier;

    public NuGetWorkspaceService(string? workspaceDirectory = null)
    {
        _workspaceDirectory = Path.GetFullPath(workspaceDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data", "NuGet"));
        _packageCacheDirectory = Path.Combine(_workspaceDirectory, "packages");
        _httpCacheDirectory = Path.Combine(_workspaceDirectory, "http-cache");
        _pluginCacheDirectory = Path.Combine(_workspaceDirectory, "plugin-cache");
        _dotnetHomeDirectory = Path.Combine(_workspaceDirectory, ".dotnet");
        _projectPath = Path.Combine(_workspaceDirectory, "ScriptPackages.csproj");
        _assetsPath = Path.Combine(_workspaceDirectory, "obj", "project.assets.json");
        _restoreRequired = EnsureProject();
    }

    public async Task<IReadOnlyList<NuGetPackageItem>> SearchAsync(string searchText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var url = $"{NuGetSearchEndpoint}?q={Uri.EscapeDataString(searchText.Trim())}&prerelease=false&take=30";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(item => new NuGetPackageItem(
                item.GetProperty("id").GetString() ?? string.Empty,
                item.GetProperty("version").GetString() ?? string.Empty,
                ReadText(item, "description"),
                ReadText(item, "authors"),
                item.TryGetProperty("totalDownloads", out var downloads) ? downloads.GetInt64() : 0))
            .Where(package => package.Id.Length > 0 && package.Version.Length > 0)
            .ToArray();
    }

    public IReadOnlyList<InstalledPackageItem> GetInstalledPackages()
    {
        var project = XDocument.Load(_projectPath);
        return project.Descendants("PackageReference")
            .Select(reference => new InstalledPackageItem(
                reference.Attribute("Include")?.Value ?? string.Empty,
                reference.Attribute("Version")?.Value ?? string.Empty))
            .Where(package => package.Id.Length > 0)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string> InstallAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        UpdatePackageReference(packageId, version);
        return await RestoreAsync(cancellationToken);
    }

    public async Task<string> RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        var project = XDocument.Load(_projectPath);
        var reference = project.Descendants("PackageReference")
            .FirstOrDefault(item => string.Equals(
                item.Attribute("Include")?.Value,
                packageId,
                StringComparison.OrdinalIgnoreCase));
        reference?.Remove();
        project.Save(_projectPath);
        return await RestoreAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetScriptReferencePathsAsync(
        string code,
        CancellationToken cancellationToken,
        string? scriptPath = null)
    {
        var packageReferences = await GetCompileReferencePathsAsync(cancellationToken);
        return packageReferences
            .Concat(GetFrameworkReferencePaths(code))
            .Concat(GetFileReferencePaths(code, scriptPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetFileReferencePaths(string code, string? scriptPath)
    {
        var directory = string.IsNullOrWhiteSpace(scriptPath) ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(scriptPath))!;
        var root = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script)).GetRoot();
        foreach (var reference in root.DescendantTrivia().Select(trivia => trivia.GetStructure())
                     .OfType<ReferenceDirectiveTriviaSyntax>().Where(reference => reference.IsActive))
        {
            var value = reference.File.ValueText;
            if (value.StartsWith("nuget:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("framework:", StringComparison.OrdinalIgnoreCase)) continue;
            var path = Path.GetFullPath(Path.Combine(directory, value));
            if (File.Exists(path) && TryGetAssemblyName(path, out _)) yield return path;
        }
    }

    public static bool UsesAspNetCoreFramework(string code) =>
        FrameworkDirectiveRegex().Matches(code)
            .Cast<Match>()
            .Any(match => string.Equals(
                match.Groups[1].Value.Trim(),
                AspNetCoreFrameworkName,
                StringComparison.OrdinalIgnoreCase));

    public static bool UsesConsoleFramework(string code) =>
        FrameworkDirectiveRegex().Matches(code)
            .Cast<Match>()
            .Any(match => string.Equals(
                match.Groups[1].Value.Trim(),
                ConsoleFrameworkName,
                StringComparison.OrdinalIgnoreCase));

    public Task<IReadOnlyList<string>> GetCompileReferencePathsAsync(CancellationToken cancellationToken) =>
        GetCompileReferencePathsAsync(null, cancellationToken);

    public Task<IReadOnlyList<string>> GetPackageCompileReferencePathsAsync(
        string packageId,
        CancellationToken cancellationToken) =>
        GetCompileReferencePathsAsync(packageId, cancellationToken);

    private async Task<IReadOnlyList<string>> GetCompileReferencePathsAsync(
        string? packageId,
        CancellationToken cancellationToken)
    {
        if (GetInstalledPackages().Count == 0)
        {
            return [];
        }

        if (_restoreRequired || !File.Exists(_assetsPath))
        {
            await RestoreAsync(cancellationToken);
        }

        await using var stream = File.OpenRead(_assetsPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var packageFolders = new[] { _packageCacheDirectory }
            .Concat(root.GetProperty("packageFolders").EnumerateObject().Select(property => property.Name)).ToArray();
        var libraries = root.GetProperty("libraries");
        var target = SelectTarget(root.GetProperty("targets"));
        if (target is null)
        {
            return [];
        }

        var compatibleRids = ReadCompatibleRuntimeIdentifiers(root);
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in target.Value.EnumerateObject())
        {
            var separatorIndex = library.Name.LastIndexOf('/');
            var libraryId = separatorIndex > 0 ? library.Name[..separatorIndex] : library.Name;
            if (packageId is not null &&
                !string.Equals(libraryId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!libraries.TryGetProperty(library.Name, out var libraryMetadata) ||
                !libraryMetadata.TryGetProperty("path", out var packagePathElement))
            {
                continue;
            }

            var packagePath = packagePathElement.GetString();
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                continue;
            }

            AddAssets(paths, packageFolders, packagePath, library.Value, "compile");
            AddAssets(paths, packageFolders, packagePath, library.Value, "runtime");

            if (!library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets))
            {
                continue;
            }

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

    private bool EnsureProject()
    {
        Directory.CreateDirectory(_workspaceDirectory);
        XDocument project;
        var created = !File.Exists(_projectPath);
        if (!created)
        {
            project = XDocument.Load(_projectPath);
        }
        else
        {
            project = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup"),
                    new XElement("ItemGroup")));
        }

        var propertyGroup = project.Root!.Elements("PropertyGroup").FirstOrDefault();
        if (propertyGroup is null)
        {
            propertyGroup = new XElement("PropertyGroup");
            project.Root.AddFirst(propertyGroup);
        }

        var settingsChanged = created ||
                              !string.Equals(
                                  propertyGroup.Element("TargetFramework")?.Value,
                                  "net8.0",
                                  StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(
                                  propertyGroup.Element("RuntimeIdentifier")?.Value,
                                  RuntimeIdentifier,
                                  StringComparison.OrdinalIgnoreCase);
        propertyGroup.SetElementValue("TargetFramework", "net8.0");
        propertyGroup.SetElementValue("ImplicitUsings", "enable");
        propertyGroup.SetElementValue("RestorePackagesPath", _packageCacheDirectory);
        propertyGroup.SetElementValue("RuntimeIdentifier", RuntimeIdentifier);
        project.Save(_projectPath);
        return settingsChanged;
    }

    private static string ReadText(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => element.GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<string> GetFrameworkReferencePaths(string code)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FrameworkDirectiveRegex().Matches(code))
        {
            var frameworkName = match.Groups[1].Value.Trim();
            if (string.Equals(frameworkName, ConsoleFrameworkName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(frameworkName, AspNetCoreFrameworkName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Framework '{frameworkName}' is not supported. " +
                    $"Use '{AspNetCoreFrameworkName}' or '{ConsoleFrameworkName}'.");
            }

            paths.UnionWith(AspNetCoreReferencePaths.Value);
        }

        return paths.ToArray();
    }

    private static IReadOnlyList<string> FindAspNetCoreReferencePaths()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(WebApplication).Assembly.Location);
        if (string.IsNullOrWhiteSpace(frameworkDirectory) || !Directory.Exists(frameworkDirectory))
        {
            throw new InvalidOperationException(
                $"The {AspNetCoreFrameworkName} shared framework is not available.");
        }

        return Directory.EnumerateFiles(frameworkDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(IsManagedAssembly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            _ = System.Reflection.AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }

    private void UpdatePackageReference(string packageId, string version)
    {
        var project = XDocument.Load(_projectPath);
        var reference = project.Descendants("PackageReference")
            .FirstOrDefault(item => string.Equals(
                item.Attribute("Include")?.Value,
                packageId,
                StringComparison.OrdinalIgnoreCase));

        if (reference is null)
        {
            var itemGroup = project.Root!.Elements("ItemGroup").FirstOrDefault() ?? new XElement("ItemGroup");
            if (itemGroup.Parent is null)
            {
                project.Root.Add(itemGroup);
            }

            itemGroup.Add(new XElement("PackageReference",
                new XAttribute("Include", packageId),
                new XAttribute("Version", version)));
        }
        else
        {
            reference.SetAttributeValue("Version", version);
        }

        project.Save(_projectPath);
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
        startInfo.ArgumentList.Add("--runtime");
        startInfo.ArgumentList.Add(RuntimeIdentifier);
        startInfo.Environment["NUGET_PACKAGES"] = _packageCacheDirectory;
        startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = _httpCacheDirectory;
        startInfo.Environment["NUGET_PLUGINS_CACHE_PATH"] = _pluginCacheDirectory;
        startInfo.Environment["DOTNET_CLI_HOME"] = _dotnetHomeDirectory;
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet restore.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"NuGet restore failed.{Environment.NewLine}{error}{Environment.NewLine}{output}");
        }

        _restoreRequired = false;
        return string.Join(Environment.NewLine, new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
    }

    private static JsonElement? SelectTarget(JsonElement targets)
    {
        var allTargets = targets.EnumerateObject().ToArray();
        var runtimeTarget = allTargets.FirstOrDefault(item =>
            item.Name.EndsWith('/' + RuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
        if (runtimeTarget.Value.ValueKind != JsonValueKind.Undefined)
        {
            return runtimeTarget.Value;
        }

        var frameworkTarget = allTargets.FirstOrDefault(item => !item.Name.Contains('/'));
        return frameworkTarget.Value.ValueKind == JsonValueKind.Undefined ? null : frameworkTarget.Value;
    }

    private static Dictionary<string, int> ReadCompatibleRuntimeIdentifiers(JsonElement root)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeIdentifier] = 0
        };
        if (!root.TryGetProperty("runtimes", out var runtimes))
        {
            return result;
        }

        var pending = new Queue<string>();
        pending.Enqueue(RuntimeIdentifier);
        while (pending.TryDequeue(out var current))
        {
            if (!runtimes.TryGetProperty(current, out var runtime))
            {
                continue;
            }

            var nextDistance = result[current] + 1;
            foreach (var importedRid in EnumerateRuntimeImports(runtime))
            {
                if (string.IsNullOrWhiteSpace(importedRid) || result.ContainsKey(importedRid))
                {
                    continue;
                }

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
        if (imports.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

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
        if (!library.TryGetProperty(groupName, out var assets))
        {
            return;
        }

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
        if (assetPath.EndsWith("_._", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var packageFolder in packageFolders)
        {
            var fullPath = Path.GetFullPath(Path.Combine(packageFolder, packagePath, assetPath));
            if (!File.Exists(fullPath) || !TryGetAssemblyName(fullPath, out var assemblyName))
            {
                continue;
            }

            paths[assemblyName] = fullPath;
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

    [GeneratedRegex("^\\s*#r\\s+\"framework:\\s*([^\"]+)\"\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex FrameworkDirectiveRegex();
}
