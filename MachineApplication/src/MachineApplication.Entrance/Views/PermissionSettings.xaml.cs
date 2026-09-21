using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>独立权限配置界面，只录入权限等级。</summary>
public partial class PermissionSettings : UserControl
{
    /// <summary>绑定权限配置模型。</summary>
    public PermissionSettings(PermissionSettingsViewModel model) { InitializeComponent(); DataContext = model; }
}
