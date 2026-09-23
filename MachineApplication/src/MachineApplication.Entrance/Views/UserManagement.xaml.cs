using System.Windows;
using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>独立用户管理界面，负责账号、密码与等级分配。</summary>
public partial class UserManagement : UserControl
{
    /// <summary>绑定用户管理模型。</summary>
    public UserManagement(UserManagementViewModel model) { InitializeComponent(); DataContext = model; }
}
