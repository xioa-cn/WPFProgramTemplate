using System.Windows;
using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>账号新增、编辑内容；密码只在保存时传递，不存入 ViewModel。</summary>
public partial class UserEditorDialog : UserControl
{
    public UserEditorDialog() => InitializeComponent();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        NewPassword?.Clear();
    }

    private void SaveUserClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is not UserManagementViewModel model) return;
        model.SaveUser(NewPassword.Password);
        if (!model.IsEditorOpen) NewPassword.Clear();
    }
}
