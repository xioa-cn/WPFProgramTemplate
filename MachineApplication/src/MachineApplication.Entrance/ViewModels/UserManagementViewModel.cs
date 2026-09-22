using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;

namespace MachineApplication.Entrance.ViewModels;

public partial class UserManagementViewModel : ObservableObject
{
    private readonly PermissionService _service;
    private int? _editingId;
    public ObservableCollection<PermissionUser> Users { get; } = [];
    public ObservableCollection<PermissionLevel> Levels { get; } = [];
    [ObservableProperty] private PermissionUser? _selectedUser;
    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private PermissionLevel? _userLevel;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _editorError = "";
    [ObservableProperty] private string _editorTitle = "";
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private bool _isOrdinaryUser = true;

    public UserManagementViewModel(PermissionService service)
    {
        _service = service;
        System.Windows.WeakEventManager<PermissionService, EventArgs>.AddHandler(service, nameof(service.CatalogChanged), OnCatalogChanged);
        Reload();
    }

    private void OnCatalogChanged(object? sender, EventArgs args) => RefreshLevels();

    public void RefreshLevels()
    {
        try
        {
            var items = _service.Levels();
            var selectedId = UserLevel?.Id;
            Levels.Clear();
            foreach (var item in items) Levels.Add(item);
            UserLevel = Levels.FirstOrDefault(x => x.Id == selectedId);
        }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }

    private void LoadUsers(int? selectedId = null)
    {
        var levels = _service.Levels();
        var users = _service.Users();
        Levels.Clear();
        foreach (var item in levels) Levels.Add(item);
        Users.Clear();
        foreach (var user in users)
        {
            user.Level = levels.FirstOrDefault(x => x.Id == user.LevelId);
            Users.Add(user);
        }
        SelectedUser = Users.FirstOrDefault(x => x.Id == selectedId) ?? Users.FirstOrDefault();
    }

    [RelayCommand]
    private void Reload()
    {
        try { LoadUsers(SelectedUser?.Id); Status = ""; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }

    [RelayCommand] private void NewUser() => OpenEditor(null);
    [RelayCommand] private void EditUser(PermissionUser? user)
    {
        if (user is not null) OpenEditor(user);
    }

    private void OpenEditor(PermissionUser? user)
    {
        RefreshLevels();
        _editingId = user?.Id;
        UserName = user?.Name ?? "";
        UserLevel = Levels.FirstOrDefault(x => x.Id == user?.LevelId);
        IsOrdinaryUser = user?.IsAdministrator != true;
        EditorTitle = user is null ? "新增用户" : "编辑用户";
        EditorError = "";
        IsEditorOpen = true;
    }

    [RelayCommand] private void CloseEditor() => IsEditorOpen = false;

    public void SaveUser(string password)
    {
        var name = UserName.Trim();
        if (string.IsNullOrWhiteSpace(name)) { EditorError = "请输入账号名称。"; return; }
        if (_editingId is null && string.IsNullOrWhiteSpace(password)) { EditorError = "新增用户必须设置密码。"; return; }
        if (IsOrdinaryUser && UserLevel is null) { EditorError = "请选择权限等级；若无可选等级，请先在权限页面新增。"; return; }
        if (Users.Any(x => x.Id != _editingId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        { EditorError = "该账号已存在。"; return; }
        try
        {
            _service.SaveUser(_editingId, name, password, UserLevel?.Id);
            IsEditorOpen = false;
            Status = "账号已保存。";
        }
        catch (Exception ex) { EditorError = ex.GetBaseException().Message; return; }
        try
        {
            LoadUsers(_editingId);
            SelectedUser = Users.FirstOrDefault(x => x.Name == name) ?? SelectedUser;
        }
        catch (Exception ex) { Status = "已保存，但列表刷新失败：" + ex.GetBaseException().Message; }
    }

    [RelayCommand]
    private void DeleteUser(PermissionUser? user)
    {
        if (user is null) return;
        try { _service.DeleteUser(user.Id); Status = $"已删除账号“{user.Name}”。"; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; return; }
        try { LoadUsers(); }
        catch (Exception ex) { Status = "已删除，但列表刷新失败：" + ex.GetBaseException().Message; }
    }
}
