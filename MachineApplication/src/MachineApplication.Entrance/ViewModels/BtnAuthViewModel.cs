using Machine.ModuleLoad.Mapper.Entity;
using Microsoft.Extensions.DependencyInjection;

namespace MachineApplication.Entrance.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Mvvm;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Utils;
using Microsoft.Extensions.DependencyInjection;

/// <summary>管理标记页面、按钮及等级授权，编辑中的按钮保存或放弃后才允许切换。</summary>
public partial class BtnAuthViewModel : ObservableObject, IAsyncNavigationAware
{
    private readonly PermissionService _permissions;
    private readonly INavigationService _navigation;
    private readonly IServiceProvider _provider;
    private readonly Dictionary<string, ButtonPermissionRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private List<PermissionLevel> _levelCatalog = [];
    private bool _restoring;
    private int _loadVersion;
    private Task _pageLoadTask = Task.CompletedTask;

    public ObservableCollection<ButtonAuthPageItem> Pages { get; } = [];
    public ObservableCollection<ButtonPermissionDescriptor> Buttons { get; } = [];
    public ObservableCollection<ButtonAuthLevelItem> Levels { get; } = [];
    [ObservableProperty] private ButtonAuthPageItem? _selectedPage;
    [ObservableProperty] private ButtonPermissionDescriptor? _selectedButton;
    [ObservableProperty] private bool _isRestricted;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "";

    public bool CanBrowse => !IsBusy && !IsDirty;
    public bool CanEdit => !IsBusy && SelectedButton?.CanConfigure == true;
    public bool CanSelectLevels => CanEdit && IsRestricted;
    public string SelectionHint => SelectedButton is null ? ViewModelLocator.EntranceLang.ButtonAuth_SelectButton :
        SelectedButton.CanConfigure ? ViewModelLocator.EntranceLang.ButtonAuth_RuleHint :
        ViewModelLocator.EntranceLang.ButtonAuth_MissingId;

    /// <summary>只接收服务依赖，页面目录在导航准备阶段异步加载。</summary>
    public BtnAuthViewModel(PermissionService permissions, INavigationService navigation, IServiceProvider provider)
    {
        _permissions = permissions;
        _navigation = navigation;
        _provider = provider;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang,
            (_, _) => OnPropertyChanged(nameof(SelectionHint)), string.Empty);
    }

    /// <summary>在导航遮挡期间读取页面目录，不批量创建所有页面。</summary>
    public async Task PrepareAsync(RegionNavigationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Pages.Count == 0 && CanBrowse) await ReloadCommand.ExecuteAsync(null);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>扫描当前已加载程序集中的标记页面，并尽量恢复原选中项。</summary>
    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task ReloadAsync()
    {
        var previousType = SelectedPage?.ViewType;
        ButtonAuthPageItem? nextPage = null;
        IsBusy = true;
        Status = ViewModelLocator.EntranceLang.ButtonAuth_Loading;
        ++_loadVersion;
        try
        {
            _permissions.Demand("page:btn/auth");
            var types = await Task.Run(() => BtnAuthAttribute.GetMarkedTypes());
            var routes = _navigation.GetRegisteredRoutes();
            SelectedPage = null;
            Pages.Clear();
            foreach (var type in types)
            {
                var route = routes.FirstOrDefault(item => item.ViewType == type);
                Pages.Add(new ButtonAuthPageItem(type, route?.Url ?? "", route?.ModuleName));
            }
            nextPage = Pages.FirstOrDefault(item => item.ViewType == previousType) ?? Pages.FirstOrDefault();
            Status = Pages.Count == 0 ? ViewModelLocator.EntranceLang.ButtonAuth_NoPages : "";
        }
        catch (Exception exception) { Status = ManagementMessages.Error(exception); }
        finally { IsBusy = false; }
        SelectedPage = nextPage;
        await _pageLoadTask;
    }

    /// <summary>递增加载版本，避免旧页面的异步结果覆盖新选中页面。</summary>
    partial void OnSelectedPageChanged(ButtonAuthPageItem? value)
    {
        ++_loadVersion;
        SelectedButton = null;
        Buttons.Clear();
        _pageLoadTask = value is not null ? LoadPageAsync(value, _loadVersion) : Task.CompletedTask;
    }

    /// <summary>仅在 UI 线程创建独立检查实例，不挂载、不导航、不触发 Loaded 或命令。</summary>
    private async Task LoadPageAsync(ButtonAuthPageItem page, int version)
    {
        IsBusy = true;
        Status = ViewModelLocator.EntranceLang.ButtonAuth_Loading;
        try
        {
            _permissions.Demand("page:btn/auth");
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (version != _loadVersion) return;
            var attribute = page.ViewType.GetCustomAttributes(true).FirstOrDefault(item =>
                item.GetType().Name.StartsWith("ModuleDataContextAttribute", StringComparison.Ordinal));
            var moduleName = page.ModuleName ?? (string?)attribute?.GetType()
                .GetProperty("ViewModelModuleName")?.GetValue(attribute);
            var provider = string.IsNullOrWhiteSpace(moduleName) ? _provider : ModuleProvider.GetModuleProvider(moduleName);
            IReadOnlyList<ButtonPermissionDescriptor> descriptors;
            using (var scope = provider.CreateScope())
            {
                // 不解析已注册的页面实例，防止意外借用当前窗口里的单例或缓存页面。
                var instance = (DependencyObject)ActivatorUtilities.CreateInstance(scope.ServiceProvider, page.ViewType);
                try { descriptors = ButtonAuthorization.Describe(instance); }
                finally { (instance as IDisposable)?.Dispose(); }
            }
            var keys = descriptors.Where(item => item.Key is not null).Select(item => item.Key!).ToArray();
            var data = await Task.Run(() => (Levels: _permissions.Levels(), Rules: _permissions.GetButtonPermissions(keys)));
            if (version != _loadVersion) return;
            _levelCatalog = data.Levels;
            _rules.Clear();
            foreach (var rule in data.Rules) _rules.Add(rule.Key, rule);
            foreach (var descriptor in descriptors) Buttons.Add(descriptor);
            SelectedButton = Buttons.FirstOrDefault();
            Status = Buttons.Count == 0 ? ViewModelLocator.EntranceLang.ButtonAuth_NoButtons : "";
        }
        catch (Exception exception)
        {
            if (version == _loadVersion) Status = ManagementMessages.Error(exception);
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    partial void OnSelectedButtonChanged(ButtonPermissionDescriptor? value) => RestoreEditor();
    partial void OnIsRestrictedChanged(bool value)
    {
        if (!_restoring) IsDirty = true;
        UpdateCommands();
    }
    partial void OnIsDirtyChanged(bool value) => UpdateCommands();
    partial void OnIsBusyChanged(bool value) => UpdateCommands();

    /// <summary>从已读取快照回填勾选项，不因切换或放弃修改而写入数据库。</summary>
    private void RestoreEditor()
    {
        _restoring = true;
        try
        {
            foreach (var level in Levels) level.PropertyChanged -= OnLevelChanged;
            Levels.Clear();
            var rule = SelectedButton?.Key is { } key ? _rules.GetValueOrDefault(key) : null;
            IsRestricted = rule is not null;
            foreach (var level in _levelCatalog)
            {
                var item = new ButtonAuthLevelItem(level.Id, level.Name, rule?.LevelIds.Contains(level.Id) == true);
                item.PropertyChanged += OnLevelChanged;
                Levels.Add(item);
            }
            IsDirty = false;
        }
        finally { _restoring = false; }
        UpdateCommands();
        OnPropertyChanged(nameof(SelectionHint));
    }

    private void OnLevelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (!_restoring && args.PropertyName == nameof(ButtonAuthLevelItem.IsAllowed)) IsDirty = true;
    }

    private bool CanSave() => CanEdit && IsDirty;

    /// <summary>锁定当前编辑目标，提交成功后才替换快照，失败时保留用户输入。</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedButton?.Key is not { } key) return;
        var button = SelectedButton;
        var rule = new ButtonPermissionRule(key, button.Name.Length > 200 ? button.Name[..200] : button.Name,
            IsRestricted, Levels.Where(item => item.IsAllowed).Select(item => item.Id).ToArray());
        IsBusy = true;
        try
        {
            await Task.Run(() => _permissions.SaveButtonPermission(rule));
            if (rule.IsRestricted) _rules[key] = rule;
            else _rules.Remove(key);
            IsDirty = false;
            Status = ViewModelLocator.EntranceLang.ButtonAuth_Saved;
        }
        catch (Exception exception) { Status = ManagementMessages.Error(exception); }
        finally { IsBusy = false; }
    }

    /// <summary>放弃当前按钮尚未保存的修改。</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Revert()
    {
        RestoreEditor();
        Status = "";
    }

    /// <summary>同步列表锁定、编辑器和命令的可用状态。</summary>
    private void UpdateCommands()
    {
        OnPropertyChanged(nameof(CanBrowse));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSelectLevels));
        ReloadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>页面目录显示类型全名及路由，区分不同模块内的同名页面。</summary>
public sealed record ButtonAuthPageItem(Type ViewType, string Route, string? ModuleName)
{
    public string Name => ViewType.Name;
    public string Description => $"{ViewType.FullName}\n{Route}";
}

/// <summary>独立的等级编辑状态，不直接修改数据库实体。</summary>
public partial class ButtonAuthLevelItem(int id, string name, bool isAllowed) : ObservableObject
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] private bool _isAllowed = isAllowed;
}
