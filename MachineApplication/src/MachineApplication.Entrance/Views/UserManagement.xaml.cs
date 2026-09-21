using System.Windows;
using System.Windows.Controls;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>独立用户管理界面，负责账号、密码与等级分配。</summary>
public partial class UserManagement : UserControl, INavigationAware
{
    /// <summary>绑定用户管理模型。</summary>
    public UserManagement(UserManagementViewModel model) { InitializeComponent(); DataContext = model; }
    public bool IsNavigationTarget(RegionNavigationContext context) => true;
    /// <summary>缓存复用时刷新可分配的权限等级。</summary>
    public void OnNavigatedTo(RegionNavigationContext context)
    {
        if (DataContext is UserManagementViewModel model) model.RefreshLevels();
    }
    public void OnNavigatedFrom() { }
    /// <summary>只在保存时传递一次性密码，操作后立即清空输入。</summary>
    private void SaveUserClick(object sender, RoutedEventArgs args)
    {
        ((UserManagementViewModel)DataContext).SaveUser(NewPassword.Password);
        NewPassword.Clear();
    }
}
