using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Models;
using MachineApplication.Entrance.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class RouterConfigurationTests
{
    [Fact]
    public void RegisteredPagesReflectRegistrationsAndReplacements()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var navigation = new NavigationService(provider, new RegionManager(provider));
        navigation.Register("home", typeof(Page), "Common");
        navigation.Register("home", typeof(UserControl), "Other");
        var route = Assert.Single(navigation.GetRegisteredRoutes());
        Assert.Equal("home", route.Url);
        Assert.Equal(typeof(UserControl), route.ViewType);
        Assert.Equal("Other", route.ModuleName);
        navigation.Register("settings", typeof(Page));
        Assert.Equal(2, navigation.GetRegisteredRoutes().Count);
    }

    [Fact]
    public void EditorPreservesNestedRoutesIconsAndTranslations()
    {
        var source = new NavigationItemConfiguration
        {
            LanguageKey = "Group", Icon = PackIconKind.CogOutline,
            Titles = new() { ["zh"] = "设置", ["en"] = "Settings" },
            Children = [new() { LanguageKey = "Home", Icon = PackIconKind.Home, Url = "home",
                Titles = new() { ["zh"] = "主页", ["en"] = "Home", ["ja"] = "ホーム", ["fr"] = "Accueil" } }]
        };
        var editor = RouteEditorNode.From(source);
        editor.Children[0].Titles.Single(title => title.Culture == "en").Title = "Dashboard";
        var config = new RouterConfiguration { NavigationItems = [editor.ToConfiguration()] };
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter<PackIconKind>());
        var json = JsonSerializer.Serialize(config, options);
        var restored = JsonSerializer.Deserialize<RouterConfiguration>(json, options)!;
        var group = Assert.Single(restored.NavigationItems);
        Assert.Null(group.Url);
        Assert.Equal("设置", group.Titles["zh"]);
        var child = Assert.Single(group.Children);
        Assert.Equal("home", child.Url);
        Assert.Equal(PackIconKind.Home, child.Icon);
        Assert.Equal("Dashboard", child.Titles["en"]);
        Assert.Equal("主页", child.Titles["zh"]);
        Assert.Equal("ホーム", child.Titles["ja"]);
        Assert.Equal("Accueil", child.Titles["fr"]);
    }
    [Fact]
    public void LanguagesCanBeAddedAndRemovedWithoutDefaultLanguages()
    {
        var editor = new RouteEditorNode();
        editor.AddLanguageCommand.Execute(null);
        var title = Assert.Single(editor.Titles);
        title.Culture = "de";
        title.Title = "Startseite";
        Assert.Equal("Startseite", editor.ToConfiguration().Titles["de"]);
        Assert.Single(editor.ToConfiguration().Titles);
        editor.RemoveLanguageCommand.Execute(title);
        Assert.Empty(editor.ToConfiguration().Titles);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("JA")]
    [InlineData(" ja ")]
    public void EmptyOrDuplicateLanguageCodesAreRejected(string culture)
    {
        var editor = new RouteEditorNode();
        editor.Titles.Add(new RouteTitleEditor { Culture = "ja", Title = "ホーム" });
        editor.Titles.Add(new RouteTitleEditor { Culture = culture, Title = "Title" });
        Assert.Throws<InvalidOperationException>(() => editor.ToConfiguration());
    }}