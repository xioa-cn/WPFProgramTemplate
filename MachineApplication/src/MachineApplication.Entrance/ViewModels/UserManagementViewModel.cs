using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>用户管理模型，只访问账号数据与可分配等级，不读取或修改授权明细。</summary>
public partial class UserManagementViewModel : ObservableObject
{
    private readonly PermissionService _service;
    public ObservableCollection<PermissionUser> Users { get; } = [];
    public ObservableCollection<PermissionLevel> Levels { get; } = [];
    [ObservableProperty] private PermissionUser? _selectedUser;
    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private PermissionLevel? _userLevel;
    [ObservableProperty] private string _status = "";

    /// <summary>读取用户管理所需数据，并在权限目录变化时刷新可分配等级。</summary>
    public UserManagementViewModel(PermissionService service)
    {
        _service = service;
        _service.CatalogChanged += (_, _) => RefreshLevels();
        Reload();
    }

    /// <summary>同步最新权限等级，保留当前正在编辑的账号。</summary>
    public void RefreshLevels()
    {
        try
        {
            var selectedId = UserLevel?.Id;
            Levels.Clear();
            foreach (var item in _service.Levels()) Levels.Add(item);
            UserLevel = Levels.FirstOrDefault(item => item.Id == selectedId);
        }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }

    /// <summary>切换账号时回填基本信息，不读取密码。</summary>
    partial void OnSelectedUserChanged(PermissionUser? value)
    {
        UserName = value?.Name ?? "";
        UserLevel = Levels.FirstOrDefault(x => x.Id == value?.LevelId);
    }
    /// <summary>统一显示校验与数据库错误。</summary>
    private void Run(Action action)
    {
        try { action(); Status = "操作成功。"; }
        catch (Exception ex) { Status = ex.GetBaseException().Message; }
    }
    /// <summary>刷新账号列表和可分配的等级。</summary>
    [RelayCommand] private void Reload() => Run(() =>
    {
        SelectedUser = null; Users.Clear(); Levels.Clear();
        foreach (var item in _service.Levels()) Levels.Add(item);
        foreach (var item in _service.Users()) Users.Add(item);
    });
    /// <summary>进入新增用户模式。</summary>
    [RelayCommand] private void NewUser() { SelectedUser = null; OnSelectedUserChanged(null); }
    /// <summary>保存账号，服务层强制检查用户保存权限。</summary>
    public void SaveUser(string password) => Run(() =>
    {
        _service.SaveUser(SelectedUser?.Id, UserName, password, UserLevel?.Id);
        Reload();
    });
    /// <summary>删除普通用户，最高权限账号不可删除。</summary>
    [RelayCommand] private void DeleteUser() => Run(() =>
    {
        if (SelectedUser is not null) _service.DeleteUser(SelectedUser.Id);
        Reload();
    });
}