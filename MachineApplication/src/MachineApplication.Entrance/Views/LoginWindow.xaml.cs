using System.Windows;
using Machine.ModuleLoad.Mapper;

namespace MachineApplication.Entrance.Views;

/// <summary>本地账号登录窗口；密码不写入绑定模型和日志。</summary>
public partial class LoginWindow : Window
{
    private readonly PermissionService _permissions;
    /// <summary>传入共享身份服务。</summary>
    public LoginWindow(PermissionService permissions)
    {
        InitializeComponent();
        _permissions = permissions;
    }
    /// <summary>验证密码后关闭窗口；校验失败保留窗口供用户重试。</summary>
    private void LoginClick(object sender, RoutedEventArgs args)
    {
        try
        {
            if (_permissions.Login(Account.Text, Password.Password)) DialogResult = true;
            else Error.Text = "账号或密码错误。";
        }
        catch (Exception ex) { Error.Text = "登录失败：" + ex.Message; }
        finally { Password.Clear(); }
    }
}