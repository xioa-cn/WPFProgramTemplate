using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>独立权限配置模型，只录入权限等级，页面访问范围在路由配置中指定。</summary>
public partial class PermissionSettingsViewModel : ObservableObject
{
    private readonly PermissionService _service;
    public ObservableCollection<PermissionLevel> Levels { get; } = [];


    [ObservableProperty] private PermissionLevel? _selectedLevel;

    [ObservableProperty] private string _levelName = "";
    [ObservableProperty] private int _rank;


    [ObservableProperty] private string _status = "";

    /// <summary>加载账号、等级及权限目录。</summary>
    public PermissionSettingsViewModel(PermissionService service) { _service = service; Reload(); }

    /// <summary>切换等级时回填名称与授权项。</summary>
    partial void OnSelectedLevelChanged(PermissionLevel? value)
    {
        LevelName = value?.Name ?? ""; Rank = value?.Rank ?? 0;

    }
    /// <summary>统一显示操作错误，避免异常传播到 WPF 消息循环。</summary>
    private void Run(Action operation)
    {
        try { operation(); Status = "操作成功。"; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }
    /// <summary>重新读取持久化数据。</summary>
    [RelayCommand]
    private void Reload() => Run(() =>
    {
        SelectedLevel = null;
        Levels.Clear();

        foreach (var item in _service.Levels()) Levels.Add(item);

    });
    /// <summary>清空等级编辑器，进入新增模式。</summary>
    [RelayCommand] private void NewLevel() { SelectedLevel = null; OnSelectedLevelChanged(null); }
    /// <summary>保存权限等级名称和排序。</summary>
    [RelayCommand] private void SaveLevel() => Run(() => { _service.SaveLevel(SelectedLevel?.Id, LevelName, Rank); Reload(); });
    /// <summary>删除未关联账号的权限等级。</summary>
    [RelayCommand] private void DeleteLevel() => Run(() => { if (SelectedLevel is not null) _service.DeleteLevel(SelectedLevel.Id); Reload(); });
}
