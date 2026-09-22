using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.Mapper.Entity;
using MachineApplication.Entrance.Utils;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>用户管理视图模型，负责账号列表、等级分配及新增编辑对话框状态。</summary>
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

    /// <summary>注入账号权限服务，弱订阅目录和语言变化并读取用户列表。</summary>
    public UserManagementViewModel(PermissionService service)
    {
        _service = service;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnDisplayLanguageChanged, string.Empty);
        System.Windows.WeakEventManager<PermissionService, EventArgs>.AddHandler(service, nameof(service.CatalogChanged), OnCatalogChanged);
        Reload();
    }

    /// <summary>响应权限等级目录变化，更新当前页面的可分配等级。</summary>
    private void OnCatalogChanged(object? sender, EventArgs args) => RefreshLevels();

    /// <summary>更新可选权限等级并保留当前正在编辑的等级选择。</summary>
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
        catch (Exception ex) { SetStatus(() => ManagementMessages.Error(ex)); }
    }

    /// <summary>读取账号与等级快照，关联等级名称并恢复列表选中项。</summary>
    private void LoadUsers(int? selectedId = null)
    {
        // 先取得账号与等级快照，再更新绑定集合，减少读取异常对现有显示的影响。
        var levels = _service.Levels();
        var users = _service.Users();
        Levels.Clear();
        foreach (var item in levels) Levels.Add(item);
        Users.Clear();
        foreach (var user in users)
        {
            // 服务返回的账号只含等级编号，这里补齐等级对象供列表显示名称。
            user.Level = levels.FirstOrDefault(x => x.Id == user.LevelId);
            Users.Add(user);
        }
        SelectedUser = Users.FirstOrDefault(x => x.Id == selectedId) ?? Users.FirstOrDefault();
    }

    /// <summary>重新读取当前页面的数据；读取失败时在状态区域显示错误。</summary>
    [RelayCommand]
    private void Reload()
    {
        try { LoadUsers(SelectedUser?.Id); SetStatus(() => ""); }
        catch (Exception ex) { SetStatus(() => ManagementMessages.Error(ex)); }
    }

    /// <summary>为指定账号打开编辑对话框，忽略空参数。</summary>
    [RelayCommand] private void NewUser() => OpenEditor(null);
    [RelayCommand] private void EditUser(PermissionUser? user)
    {
        if (user is not null) OpenEditor(user);
    }

    /// <summary>回填独立编辑状态并打开对话框，避免列表选择改变编辑目标。</summary>
    private void OpenEditor(PermissionUser? user)
    {
        RefreshLevels();
        // 单独保存编辑目标；切换列表选择不会误将修改提交给其他账号。
        _editingId = user?.Id;
        UserName = user?.Name ?? "";
        UserLevel = Levels.FirstOrDefault(x => x.Id == user?.LevelId);
        // 界面禁用管理员名称和等级编辑，最终权限限制仍由服务端逻辑强制检查。
        IsOrdinaryUser = user?.IsAdministrator != true;
        SetEditorTitle(() => user is null ? ViewModelLocator.EntranceLang.Management_NewUserTitle : ViewModelLocator.EntranceLang.Management_EditUserTitle);
        SetEditorError(() => "");
        IsEditorOpen = true;
    }

    /// <summary>关闭编辑对话框，不将尚未保存的输入写入数据库。</summary>
    [RelayCommand] private void CloseEditor() => IsEditorOpen = false;

    /// <summary>校验账号、密码和等级后保存；密码仅由视图在本次调用中传入。</summary>
    public void SaveUser(string password)
    {
        var name = UserName.Trim();
        if (string.IsNullOrWhiteSpace(name)) { SetEditorError(() => ViewModelLocator.EntranceLang.Management_NameRequired); return; }
        if (_editingId is null && string.IsNullOrWhiteSpace(password)) { SetEditorError(() => ViewModelLocator.EntranceLang.Management_PasswordRequired); return; }
        if (IsOrdinaryUser && UserLevel is null) { SetEditorError(() => ViewModelLocator.EntranceLang.Management_LevelRequired); return; }
        if (Users.Any(x => x.Id != _editingId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        { SetEditorError(() => ViewModelLocator.EntranceLang.Management_DuplicateUser); return; }
        try
        {
            _service.SaveUser(_editingId, name, password, UserLevel?.Id);
            IsEditorOpen = false;
            SetStatus(() => ViewModelLocator.EntranceLang.Management_UserSaved);
        }
        catch (Exception ex) { SetEditorError(() => ManagementMessages.Error(ex)); return; }
        try
        {
            LoadUsers(_editingId);
            SelectedUser = Users.FirstOrDefault(x => x.Name == name) ?? SelectedUser;
        }
        catch (Exception ex) { SetStatus(() => ViewModelLocator.EntranceLang.Management_SavedRefreshFailed + ManagementMessages.Error(ex)); }
    }

    /// <summary>删除指定普通账号并刷新列表，最高权限账号由服务层保护。</summary>
    [RelayCommand]
    private void DeleteUser(PermissionUser? user)
    {
        if (user is null) return;
        try { _service.DeleteUser(user.Id); SetStatus(() => string.Format(ViewModelLocator.EntranceLang.Management_UserDeleted, user.Name)); }
        catch (Exception ex) { SetStatus(() => ManagementMessages.Error(ex)); return; }
        try { LoadUsers(); }
        catch (Exception ex) { SetStatus(() => ViewModelLocator.EntranceLang.Management_DeletedRefreshFailed + ManagementMessages.Error(ex)); }
    }
    // 保留文案生成函数，使已显示的状态与错误在切换语言后仍能刷新。
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
