using Machine.ModuleLoad.Mapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad;

using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Models;
using MaterialDesignThemes.Wpf;

namespace MachineApplication.Entrance.ViewModels;

public partial class RouteTitleEditor : ObservableObject
{
    [ObservableProperty] private string _culture = "";
    [ObservableProperty] private string _title = "";
}

public partial class RouteEditorNode : ObservableObject
{
    [ObservableProperty] private string _languageKey = "Menu_New";
    public ObservableCollection<RouteTitleEditor> Titles { get; } = [];

    [RelayCommand]
    private void AddLanguage() => Titles.Add(new RouteTitleEditor());

    [RelayCommand]
    private void RemoveLanguage(RouteTitleEditor? title)
    {
        if (title is not null) Titles.Remove(title);
    }

    private Dictionary<string, string> BuildTitles()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var translation in Titles)
        {
            var culture = translation.Culture.Trim();
            if (string.IsNullOrWhiteSpace(culture))
                throw new InvalidOperationException($"菜单 {LanguageKey} 的语言代码不能为空。");
            if (!result.TryAdd(culture, translation.Title.Trim()))
                throw new InvalidOperationException($"菜单 {LanguageKey} 的语言代码 {culture} 重复。");
        }

        return result;
    }

    [ObservableProperty] private string? _url;
    [ObservableProperty] private PackIconKind _icon = PackIconKind.FileOutline;
    [ObservableProperty] private PermissionLevel? _levelToAdd;
    public ObservableCollection<PermissionLevel> Levels { get; } = [];
    public ObservableCollection<PermissionLevel> RequiredLevels { get; } = [];
    public ObservableCollection<RouteEditorNode> Children { get; } = [];

    /// <summary>将下拉框中选中的等级加入允许访问列表，已存在则忽略。</summary>
    [RelayCommand]
    private void AddRequiredLevel()
    {
        if (LevelToAdd is not { } level || RequiredLevels.Any(item => item.Id == level.Id)) return;
        RequiredLevels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
    }

    /// <summary>从当前页面的允许访问列表中移除指定等级。</summary>
    [RelayCommand]
    private void RemoveRequiredLevel(PermissionLevel? level)
    {
        var existing = RequiredLevels.FirstOrDefault(item => item.Id == level?.Id);
        if (existing is not null) RequiredLevels.Remove(existing);
    }

    public static RouteEditorNode From(NavigationItemConfiguration item)
    {
        var node = new RouteEditorNode { LanguageKey = item.LanguageKey, Icon = item.Icon, Url = item.Url };
        foreach (var levelId in item.RequiredLevelIds) node.RequiredLevels.Add(new PermissionLevel { Id = levelId });
        foreach (var title in item.Titles)
            node.Titles.Add(new RouteTitleEditor { Culture = title.Key, Title = title.Value });
        foreach (var child in item.Children) node.Children.Add(From(child));
        return node;
    }

    public NavigationItemConfiguration ToConfiguration() => new()
    {
        LanguageKey = LanguageKey.Trim(), Icon = Icon, Url = Children.Count > 0 ? null : Url,
        Titles = BuildTitles(),
        Children = Children.Select(child => child.ToConfiguration()).ToList(), RequiredLevelIds = RequiredLevels.Select(x => x.Id).ToList()
    };
}

public partial class RouterSettingViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly MainWindowViewModel _main;
    private readonly PermissionService _permissions;
    public ObservableCollection<RouteEditorNode> Items { get; } = [];
    public ObservableCollection<RegisteredRoute> Pages { get; } = [];

    public IReadOnlyList<PackIconKind> Icons { get; } =
        Enum.GetValues<PackIconKind>().Distinct().OrderBy(icon => icon.ToString()).ToArray();

    [ObservableProperty] private RouteEditorNode? _selectedItem;
    [ObservableProperty] private string _status = "";

    public RouterSettingViewModel(INavigationService navigation, MainWindowViewModel main, PermissionService permissions)
    {
        _navigation = navigation;
        _main = main;
        _permissions = permissions;
        _permissions.CatalogChanged += OnCatalogChanged;
        Reload();
    }

    private void OnCatalogChanged(object? sender, EventArgs args) => RefreshLevels();

    /// <summary>同步最新权限等级，不丢弃尚未保存的菜单编辑。</summary>
    public void RefreshLevels()
    {
        try
        {
            ApplyLevels(_permissions.Levels());
        }
        catch (Exception ex)
        {
            Status = "刷新权限等级失败：" + ex.Message;
        }
    }

    private IReadOnlyList<PermissionLevel> CurrentLevels()
    {
        try { return _permissions.Levels(); }
        catch { return []; }
    }

    private void ApplyLevels(IReadOnlyList<PermissionLevel> levels)
    {
        void Sync(IEnumerable<RouteEditorNode> nodes)
        {
            foreach (var node in nodes)
            {
                var selectedId = node.LevelToAdd?.Id;
                node.Levels.Clear();
                foreach (var level in levels)
                    node.Levels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
                var requiredIds = node.RequiredLevels.Select(item => item.Id).ToList();
                node.RequiredLevels.Clear();
                foreach (var id in requiredIds)
                {
                    var match = levels.FirstOrDefault(item => item.Id == id);
                    node.RequiredLevels.Add(match is null
                        ? new PermissionLevel { Id = id, Name = $"已删除等级 {id}" }
                        : new PermissionLevel { Id = match.Id, Name = match.Name, Rank = match.Rank });
                }
                node.LevelToAdd = node.Levels.FirstOrDefault(item => item.Id == selectedId) ?? node.Levels.FirstOrDefault();
                Sync(node.Children);
            }
        }
        Sync(Items);
    }

    [RelayCommand]
    private void RefreshPages()
    {
        Pages.Clear();
        foreach (var route in _navigation.GetRegisteredRoutes()) Pages.Add(route);
    }

    [RelayCommand]
    private void Reload()
    {
        try
        {
            var config = RouterConfiguration.Read();
            Items.Clear();
            foreach (var item in config.NavigationItems) Items.Add(RouteEditorNode.From(item));
            ApplyLevels(CurrentLevels());
            SelectedItem = Items.FirstOrDefault();
            RefreshPages();
            Status = "已加载配置。留空的语言标题使用语言键对应的现有翻译。";
        }
        catch (Exception ex)
        {
            Status = "加载失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void AddRoot()
    {
        var node = new RouteEditorNode();
        foreach (var level in CurrentLevels()) node.Levels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
        node.LevelToAdd = node.Levels.FirstOrDefault();
        Items.Add(node);
        SelectedItem = node;
    }

    [RelayCommand]
    private void AddChild()
    {
        if (SelectedItem is null) return;
        var node = new RouteEditorNode();
        foreach (var level in CurrentLevels()) node.Levels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
        node.LevelToAdd = node.Levels.FirstOrDefault();
        SelectedItem.Children.Add(node);
        SelectedItem.Url = null;
        SelectedItem = node;
    }

    private ObservableCollection<RouteEditorNode>? FindParent(ObservableCollection<RouteEditorNode> items,
        RouteEditorNode node)
    {
        if (items.Contains(node)) return items;
        foreach (var item in items)
            if (FindParent(item.Children, node) is { } parent)
                return parent;
        return null;
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is not { } node) return;
        FindParent(Items, node)?.Remove(node);
        SelectedItem = Items.FirstOrDefault();
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int offset)
    {
        if (SelectedItem is not { } node || FindParent(Items, node) is not { } parent) return;
        var index = parent.IndexOf(node);
        if (index + offset >= 0 && index + offset < parent.Count) parent.Move(index, index + offset);
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            var config = new RouterConfiguration
                { NavigationItems = Items.Select(item => item.ToConfiguration()).ToList() };

            void Validate(IEnumerable<NavigationItemConfiguration> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.Children.Count == 0 &&
                        (string.IsNullOrWhiteSpace(node.Url) || !_navigation.GetRegisteredRoutes().Any(route => string.Equals(route.Url, node.Url, StringComparison.OrdinalIgnoreCase))))
                        throw new InvalidOperationException($"菜单 {node.LanguageKey} 请选择已注册页面。");
                    Validate(node.Children);
                }
            }

            Validate(config.NavigationItems);
            // 即使绕过界面直接执行命令，也必须验证保存权限。
            MainProvider.ServiceProvider!.GetRequiredService<Machine.ModuleLoad.Mapper.PermissionService>().Demand("page:settings/routes");
            RouterConfiguration.Save(config);
            _main.ReloadNavigation();
            Status = "已保存 Router.json，导航菜单已更新。";
        }
        catch (Exception ex)
        {
            Status = "保存失败：" + ex.Message;
        }
    }
}
