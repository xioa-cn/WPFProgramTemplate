using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaterialDesignThemes.Wpf;

namespace MachineApplication.Entrance.Models;

/// <summary>从程序目录的 Router.json 加载树形导航菜单。</summary>
public sealed class RouterConfiguration
{
    public static void Save(RouterConfiguration configuration)
    {
        // Validate the entire tree before replacing the existing file.
        _ = configuration.NavigationItems.Select(item => item.ToModel()).ToArray();
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter<PackIconKind>(allowIntegerValues: false));
        var path = Path.Combine(AppContext.BaseDirectory, "Router.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(configuration, options));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public required List<NavigationItemConfiguration> NavigationItems { get; init; }

    public static IReadOnlyList<NavModel> Load() => Read().NavigationItems.Select(item => item.ToModel()).ToArray();

    public static RouterConfiguration Read()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Router.json");
        using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter<PackIconKind>(allowIntegerValues: false));
        var configuration = JsonSerializer.Deserialize<RouterConfiguration>(stream, options);
        if (configuration?.NavigationItems is null)
            throw new JsonException("Router.json must contain NavigationItems.");
        return configuration;
    }
}

public sealed class NavigationItemConfiguration
{
    public required string LanguageKey { get; init; }
    public required PackIconKind Icon { get; init; }
    public Dictionary<string, string> Titles { get; init; } = new();
    public string? Url { get; init; }
    public List<NavigationItemConfiguration> Children { get; init; } = [];

    internal NavModel ToModel()
    {
        if (string.IsNullOrWhiteSpace(LanguageKey) || Children is null)
            throw new JsonException("Each navigation item requires a LanguageKey and a non-null Children collection.");
        if (Children.Count == 0 && string.IsNullOrWhiteSpace(Url))
            throw new JsonException($"Navigation item '{LanguageKey}' requires a Url or Children.");
        return new NavModel(LanguageKey, Icon, Url, Children.Select(child => child.ToModel()).ToArray()) { Titles = Titles };
    }
}