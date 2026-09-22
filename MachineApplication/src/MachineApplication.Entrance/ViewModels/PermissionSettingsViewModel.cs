using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;
using MachineApplication.Entrance.Utils;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>权限等级目录与独立对话框编辑状态。</summary>
public partial class PermissionSettingsViewModel : ObservableObject
{
    private readonly PermissionService _service;
    private int? _editingId;
    public ObservableCollection<PermissionLevel> Levels { get; } = [];
    /// <summary>注入权限服务，订阅显示语言变化并加载等级目录。</summary>
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
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnDisplayLanguageChanged, string.Empty);
        Reload();
    }

    /// <summary>先读取完整等级列表，再替换界面集合并恢复选中项。</summary>
    private void LoadLevels(int? selectedId = null)
    {
        // 先查询成功再清空集合，避免数据库读取失败后列表变空。
        var items = _service.Levels();
        Levels.Clear();
        foreach (var item in items) Levels.Add(item);
        SelectedLevel = Levels.FirstOrDefault(x => x.Id == selectedId) ?? Levels.FirstOrDefault();
    }

    /// <summary>重新读取当前页面的数据；读取失败时在状态区域显示错误。</summary>
    [RelayCommand]
    private void Reload()
    {
        try { LoadLevels(SelectedLevel?.Id); SetStatus(() => ""); }
        catch (Exception ex) { SetStatus(() => ManagementMessages.Error(ex)); }
    }

    /// <summary>以空编辑数据打开新增权限等级对话框。</summary>
    [RelayCommand]
    private void NewLevel() => OpenEditor(null);

    /// <summary>为指定权限等级打开编辑对话框，忽略空参数。</summary>
    [RelayCommand]
    private void EditLevel(PermissionLevel? level)
    {
        if (level is not null) OpenEditor(level);
    }

    /// <summary>回填独立编辑状态并打开对话框，避免列表选择改变编辑目标。</summary>
    private void OpenEditor(PermissionLevel? level)
    {
        // 编辑目标编号独立于列表选中项；空编号代表创建新记录。
        _editingId = level?.Id;
        LevelName = level?.Name ?? "";
        Rank = (level?.Rank ?? 0).ToString(CultureInfo.CurrentCulture);
        SetEditorTitle(() => level is null ? ViewModelLocator.EntranceLang.Management_NewLevelTitle : ViewModelLocator.EntranceLang.Management_EditLevelTitle);
        SetEditorError(() => "");
        IsEditorOpen = true;
    }

    /// <summary>关闭编辑对话框，不将尚未保存的输入写入数据库。</summary>
    [RelayCommand]
    private void CloseEditor() => IsEditorOpen = false;

    /// <summary>校验名称及排序，保存等级后刷新列表并定位保存的记录。</summary>
    [RelayCommand]
    private void SaveLevel()
    {
        if (string.IsNullOrWhiteSpace(LevelName)) { SetEditorError(() => ViewModelLocator.EntranceLang.Management_LevelNameRequired); return; }
        if (!int.TryParse(Rank, out var rank)) { SetEditorError(() => ViewModelLocator.EntranceLang.Management_RankInvalid); return; }
        var name = LevelName.Trim();
        if (Levels.Any(x => x.Id != _editingId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        { SetEditorError(() => ViewModelLocator.EntranceLang.Management_DuplicateLevel); return; }
        try
        {
            _service.SaveLevel(_editingId, name, rank);
            IsEditorOpen = false;
            SetStatus(() => ViewModelLocator.EntranceLang.Management_LevelSaved);
        }
        catch (Exception ex) { SetEditorError(() => ManagementMessages.Error(ex)); return; }
        // 持久化与刷新分别处理，刷新失败时明确提示数据已保存，避免重复提交。
        try
        {
            LoadLevels(_editingId);
            SelectedLevel = Levels.FirstOrDefault(x => x.Name == name) ?? SelectedLevel;
        }
        catch (Exception ex) { SetStatus(() => ViewModelLocator.EntranceLang.Management_SavedRefreshFailed + ManagementMessages.Error(ex)); }
    }

    /// <summary>删除指定等级并刷新列表；关联账号等限制由服务层验证。</summary>
    [RelayCommand]
    private void DeleteLevel(PermissionLevel? level)
    {
        if (level is null) return;
        try { _service.DeleteLevel(level.Id); SetStatus(() => string.Format(ViewModelLocator.EntranceLang.Management_LevelDeleted, level.Name)); }
        catch (Exception ex) { SetStatus(() => ManagementMessages.Error(ex)); return; }
        try { LoadLevels(); }
        catch (Exception ex) { SetStatus(() => ViewModelLocator.EntranceLang.Management_DeletedRefreshFailed + ManagementMessages.Error(ex)); }
    }
    // 保存文案计算函数而非固定翻译，切换语言时可重新生成显示内容。
    private Func<string> _statusText = () => string.Empty;
    /// <summary>保存状态文本的计算函数，立即显示并支持之后重新翻译。</summary>
    private void SetStatus(Func<string> text) { _statusText = text; Status = text(); }
    private Func<string> _editorErrorText = () => string.Empty;
    /// <summary>保存校验提示的计算函数，以便切换语言后更新已有提示。</summary>
    private void SetEditorError(Func<string> text) { _editorErrorText = text; EditorError = text(); }
    private Func<string> _editorTitleText = () => string.Empty;
    /// <summary>保存对话框标题的计算函数，使新增与编辑标题跟随语言变化。</summary>
    private void SetEditorTitle(Func<string> text) { _editorTitleText = text; EditorTitle = text(); }
    /// <summary>重新计算已显示的本地化文案，保留编辑数据及对话框状态。</summary>
    private void OnDisplayLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        Status = _statusText();
        EditorError = _editorErrorText();
        EditorTitle = _editorTitleText();
    }
}
