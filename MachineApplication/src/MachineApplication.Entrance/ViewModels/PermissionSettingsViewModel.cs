using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>权限等级目录与独立对话框编辑状态。</summary>
public partial class PermissionSettingsViewModel : ObservableObject
{
    private readonly PermissionService _service;
    private int? _editingId;
    public ObservableCollection<PermissionLevel> Levels { get; } = [];
    [ObservableProperty] private PermissionLevel? _selectedLevel;
    [ObservableProperty] private string _levelName = "";
    [ObservableProperty] private string _rank = "0";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _editorError = "";
    [ObservableProperty] private string _editorTitle = "";
    [ObservableProperty] private bool _isEditorOpen;
    public PermissionSettingsViewModel(PermissionService service)
    {
        _service = service;
        Reload();
    }

    private void LoadLevels(int? selectedId = null)
    {
        var items = _service.Levels();
        Levels.Clear();
        foreach (var item in items) Levels.Add(item);
        SelectedLevel = Levels.FirstOrDefault(x => x.Id == selectedId) ?? Levels.FirstOrDefault();
    }

    [RelayCommand]
    private void Reload()
    {
        try { LoadLevels(SelectedLevel?.Id); Status = ""; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }

    [RelayCommand]
    private void NewLevel() => OpenEditor(null);

    [RelayCommand]
    private void EditLevel(PermissionLevel? level)
    {
        if (level is not null) OpenEditor(level);
    }

    private void OpenEditor(PermissionLevel? level)
    {
        _editingId = level?.Id;
        LevelName = level?.Name ?? "";
        Rank = (level?.Rank ?? 0).ToString(CultureInfo.CurrentCulture);
        EditorTitle = level is null ? "新增权限等级" : "编辑权限等级";
        EditorError = "";
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor() => IsEditorOpen = false;

    [RelayCommand]
    private void SaveLevel()
    {
        if (string.IsNullOrWhiteSpace(LevelName)) { EditorError = "请输入等级名称。"; return; }
        if (!int.TryParse(Rank, out var rank)) { EditorError = "排序值请输入有效整数。"; return; }
        var name = LevelName.Trim();
        if (Levels.Any(x => x.Id != _editingId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        { EditorError = "该等级名称已存在，请使用其他名称。"; return; }
        try
        {
            _service.SaveLevel(_editingId, name, rank);
            IsEditorOpen = false;
            Status = "权限等级已保存。";
        }
        catch (Exception ex) { EditorError = ex.GetBaseException().Message; return; }
        try
        {
            LoadLevels(_editingId);
            SelectedLevel = Levels.FirstOrDefault(x => x.Name == name) ?? SelectedLevel;
        }
        catch (Exception ex) { Status = "已保存，但列表刷新失败：" + ex.GetBaseException().Message; }
    }

    [RelayCommand]
    private void DeleteLevel(PermissionLevel? level)
    {
        if (level is null) return;
        try { _service.DeleteLevel(level.Id); Status = $"已删除权限等级“{level.Name}”。"; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; return; }
        try { LoadLevels(); }
        catch (Exception ex) { Status = "已删除，但列表刷新失败：" + ex.GetBaseException().Message; }
    }
}
