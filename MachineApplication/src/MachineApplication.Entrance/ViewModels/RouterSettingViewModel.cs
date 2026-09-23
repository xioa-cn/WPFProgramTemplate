using Machine.ModuleLoad.Mapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad;
using Machine.ModuleLoad.Mvvm;
using Machine.ModuleLoad.Mapper.Entity;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Models;
using MachineApplication.Entrance.Utils;
using MaterialDesignThemes.Wpf;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>单条菜单翻译的编辑模型，保存语言代码与对应标题。</summary>
public partial class RouteTitleEditor : ObservableObject
{
    [ObservableProperty] private string _culture = "";
    [ObservableProperty] private string _title = "";
}

/// <summary>菜单树节点的编辑模型，维护子节点、翻译、目标页面和访问等级。</summary>
public partial class RouteEditorNode : ObservableObject
{
    [ObservableProperty] private string _languageKey = "Menu_New";
    public ObservableCollection<RouteTitleEditor> Titles { get; } = [];

    /// <summary>追加一条空翻译，由用户填写语言代码与菜单标题。</summary>
    [RelayCommand]
    private void AddLanguage() => Titles.Add(new RouteTitleEditor());

    /// <summary>移除指定翻译项，忽略空参数。</summary>
    [RelayCommand]
    private void RemoveLanguage(RouteTitleEditor? title)
    {
        if (title is not null) Titles.Remove(title);
    }

    private Dictionary<string, string> BuildTitles()
    {
        // 语言代码忽略大小写，避免 ja 与 JA 被保存为重复语言。
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var translation in Titles)
        {
            var culture = translation.Culture.Trim();
            if (string.IsNullOrWhiteSpace(culture))
                throw new InvalidOperationException(string.Format(ViewModelLocator.EntranceLang.Management_MissingCulture, LanguageKey));
            if (!result.TryAdd(culture, translation.Title.Trim()))
                throw new InvalidOperationException(string.Format(ViewModelLocator.EntranceLang.Management_DuplicateCulture, LanguageKey, culture));
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

    /// <summary>递归将持久化菜单配置复制为可绑定的编辑节点。</summary>
    public static RouteEditorNode From(NavigationItemConfiguration item)
    {
        var node = new RouteEditorNode { LanguageKey = item.LanguageKey, Icon = item.Icon, Url = item.Url };
        foreach (var levelId in item.RequiredLevelIds) node.RequiredLevels.Add(new PermissionLevel { Id = levelId });
        foreach (var title in item.Titles)
            node.Titles.Add(new RouteTitleEditor { Culture = title.Key, Title = title.Value });
        foreach (var child in item.Children) node.Children.Add(From(child));
        return node;
    }

    /// <summary>将编辑树转换为保存模型；含子项的分组不保存目标页面。</summary>
    public NavigationItemConfiguration ToConfiguration() => new()
    {
        LanguageKey = LanguageKey.Trim(), Icon = Icon, Url = Children.Count > 0 ? null : Url,
        Titles = BuildTitles(),
        Children = Children.Select(child => child.ToConfiguration()).ToList(), RequiredLevelIds = RequiredLevels.Select(x => x.Id).ToList()
    };
}

/// <summary>路由配置视图模型，管理菜单树编辑、权限目录同步及配置保存。</summary>
public partial class RouterSettingViewModel : NavigationObservableObject
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

    /// <summary>关联导航、主窗口和权限服务，订阅等级目录变化并加载路由。</summary>
    public RouterSettingViewModel(INavigationService navigation, MainWindowViewModel main, PermissionService permissions)
    {
        _navigation = navigation;
        _main = main;
        _permissions = permissions;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnDisplayLanguageChanged, string.Empty);
        _permissions.CatalogChanged += OnCatalogChanged;
        Reload();
    }

    public override bool IsNavigationTarget(RegionNavigationContext context) => true;

    /// <summary>页面被缓存复用时重新读取权限等级，避免沿用旧目录。</summary>
    public override void OnNavigatedTo(RegionNavigationContext context) => RefreshLevels();

    /// <summary>响应权限等级目录变化，更新当前页面的可分配等级。</summary>
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
            SetStatus(() => ViewModelLocator.EntranceLang.Management_LevelRefreshFailed + ManagementMessages.Error(ex));
        }
    }

    /// <summary>读取当前可用等级；读取失败时返回空目录供编辑界面展示。</summary>
    private IReadOnlyList<PermissionLevel> CurrentLevels()
    {
        try { return _permissions.Levels(); }
        catch { return []; }
    }

    /// <summary>递归同步菜单节点的等级选项，保留已配置的访问等级编号。</summary>
    private void ApplyLevels(IReadOnlyList<PermissionLevel> levels)
    {
        // 递归同步整个菜单树，保留各节点的页面编辑和授权编号。
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
                    // 已删除等级保留占位编号，避免刷新目录时悄悄丢失已有授权配置。
                    node.RequiredLevels.Add(match is null
                        ? new PermissionLevel { Id = id, Name = string.Format(ViewModelLocator.EntranceLang.Management_DeletedLevel, id) }
                        : new PermissionLevel { Id = match.Id, Name = match.Name, Rank = match.Rank });
                }
                node.LevelToAdd = node.Levels.FirstOrDefault(item => item.Id == selectedId) ?? node.Levels.FirstOrDefault();
                Sync(node.Children);
            }
        }
        Sync(Items);
    }

    /// <summary>从导航注册表重新获取页面列表，供目标页面下拉框选择。</summary>
    [RelayCommand]
    private void RefreshPages()
    {
        Pages.Clear();
        foreach (var route in _navigation.GetRegisteredRoutes()) Pages.Add(route);
    }

    /// <summary>重新读取当前页面的数据；读取失败时在状态区域显示错误。</summary>
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
            SetStatus(() => ViewModelLocator.EntranceLang.Management_RoutesLoaded);
        }
        catch (Exception ex)
        {
            SetStatus(() => ViewModelLocator.EntranceLang.Management_LoadFailed + ManagementMessages.Error(ex));
        }
    }

    /// <summary>创建顶层菜单节点，初始化等级选项并选中新节点。</summary>
    [RelayCommand]
    private void AddRoot()
    {
        var node = new RouteEditorNode();
        foreach (var level in CurrentLevels()) node.Levels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
        node.LevelToAdd = node.Levels.FirstOrDefault();
        Items.Add(node);
        SelectedItem = node;
    }

    /// <summary>为当前菜单添加子项，并清空父级目标页面使其成为分组。</summary>
    [RelayCommand]
    private void AddChild()
    {
        if (SelectedItem is null) return;
        var node = new RouteEditorNode();
        foreach (var level in CurrentLevels()) node.Levels.Add(new PermissionLevel { Id = level.Id, Name = level.Name, Rank = level.Rank });
        node.LevelToAdd = node.Levels.FirstOrDefault();
        SelectedItem.Children.Add(node);
        // 包含子菜单后父节点作为分组显示，不再直接导航到页面。
        SelectedItem.Url = null;
        SelectedItem = node;
    }

    /// <summary>递归定位包含指定节点的集合，供删除和同级排序使用。</summary>
    private ObservableCollection<RouteEditorNode>? FindParent(ObservableCollection<RouteEditorNode> items,
        RouteEditorNode node)
    {
        if (items.Contains(node)) return items;
        foreach (var item in items)
            if (FindParent(item.Children, node) is { } parent)
                return parent;
        return null;
    }

    /// <summary>从所属集合移除当前节点，并将选择移回首个顶层菜单。</summary>
    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is not { } node) return;
        FindParent(Items, node)?.Remove(node);
        SelectedItem = Items.FirstOrDefault();
    }

    /// <summary>将当前节点在同级菜单中向前移动一位。</summary>
    [RelayCommand]
    private void MoveUp() => Move(-1);

    /// <summary>将当前节点在同级菜单中向后移动一位。</summary>
    [RelayCommand]
    private void MoveDown() => Move(1);

    /// <summary>按偏移量调整同级顺序，越界时保持原位置。</summary>
    private void Move(int offset)
    {
        if (SelectedItem is not { } node || FindParent(Items, node) is not { } parent) return;
        var index = parent.IndexOf(node);
        if (index + offset >= 0 && index + offset < parent.Count) parent.Move(index, index + offset);
    }

    /// <summary>校验菜单树与操作权限，保存配置后通知主窗口重建导航。</summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            var config = new RouterConfiguration
                { NavigationItems = Items.Select(item => item.ToConfiguration()).ToList() };

            // 保存前递归校验所有叶子节点，防止配置未注册或空白的页面地址。
            void Validate(IEnumerable<NavigationItemConfiguration> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.Children.Count == 0 &&
                        (string.IsNullOrWhiteSpace(node.Url) || !_navigation.GetRegisteredRoutes().Any(route => string.Equals(route.Url, node.Url, StringComparison.OrdinalIgnoreCase))))
                        throw new InvalidOperationException(string.Format(ViewModelLocator.EntranceLang.Management_SelectPage, node.LanguageKey));
                    Validate(node.Children);
                }
            }

            Validate(config.NavigationItems);
            // 即使绕过界面直接执行命令，也必须验证保存权限。
            MainProvider.ServiceProvider!.GetRequiredService<Machine.ModuleLoad.Mapper.PermissionService>().Demand("page:settings/routes");
            RouterConfiguration.Save(config);
            _main.ReloadNavigation();
            SetStatus(() => ViewModelLocator.EntranceLang.Management_RoutesSaved);
        }
        catch (Exception ex)
        {
            SetStatus(() => ViewModelLocator.EntranceLang.Management_SaveFailed + ManagementMessages.Error(ex));
        }
    }
    // 保存状态的生成函数，让切换语言也能更新最近一次操作结果。
    private Func<string> _statusText = () => string.Empty;
    /// <summary>保存状态文本的计算函数，立即显示并支持之后重新翻译。</summary>
    private void SetStatus(Func<string> text) { _statusText = text; Status = text(); }
    /// <summary>重新计算已显示的本地化文案，保留编辑数据及对话框状态。</summary>
    private void OnDisplayLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        Status = _statusText();
    }
}
