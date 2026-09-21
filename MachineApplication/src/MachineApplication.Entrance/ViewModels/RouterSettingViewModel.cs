using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public ObservableCollection<RouteEditorNode> Children { get; } = [];

    public static RouteEditorNode From(NavigationItemConfiguration item)
    {
        var node = new RouteEditorNode { LanguageKey = item.LanguageKey, Icon = item.Icon, Url = item.Url };
        foreach (var title in item.Titles)
            node.Titles.Add(new RouteTitleEditor { Culture = title.Key, Title = title.Value });
        foreach (var child in item.Children) node.Children.Add(From(child));
        return node;
    }

    public NavigationItemConfiguration ToConfiguration() => new()
    {
        LanguageKey = LanguageKey.Trim(), Icon = Icon, Url = Children.Count > 0 ? null : Url,
        Titles = BuildTitles(),
        Children = Children.Select(child => child.ToConfiguration()).ToList()
    };
}

public partial class RouterSettingViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly MainWindowViewModel _main;
    public ObservableCollection<RouteEditorNode> Items { get; } = [];
    public ObservableCollection<RegisteredRoute> Pages { get; } = [];

    public IReadOnlyList<PackIconKind> Icons { get; } =
        Enum.GetValues<PackIconKind>().Distinct().OrderBy(icon => icon.ToString()).ToArray();

    [ObservableProperty] private RouteEditorNode? _selectedItem;
    [ObservableProperty] private string _status = "";

    public RouterSettingViewModel(INavigationService navigation, MainWindowViewModel main)
    {
        _navigation = navigation;
        _main = main;
        Reload();
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
        Items.Add(node);
        SelectedItem = node;
    }

    [RelayCommand]
    private void AddChild()
    {
        if (SelectedItem is null) return;
        var node = new RouteEditorNode();
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
                        (string.IsNullOrWhiteSpace(node.Url) || !_navigation.CanNavigate(node.Url)))
                        throw new InvalidOperationException($"菜单 {node.LanguageKey} 请选择已注册页面。");
                    Validate(node.Children);
                }
            }

            Validate(config.NavigationItems);
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