using MachineApplication.Entrance.ViewModels;
using System.Windows;
using Machine.ModuleLoad.Mapper;
using MachineApplication.Entrance.Utils;

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
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
    }
    /// <summary>验证密码后关闭窗口；校验失败保留窗口供用户重试。</summary>
    private void LoginClick(object sender, RoutedEventArgs args)
    {
        try
        {
            if (_permissions.Login(Account.Text, Password.Password)) DialogResult = true;
            else SetError(() => ViewModelLocator.EntranceLang.Management_InvalidLogin);
        }
        catch (Exception ex) { SetError(() => ViewModelLocator.EntranceLang.Management_LoginFailed + ManagementMessages.Error(ex)); }
        finally { Password.Clear(); }
    }
    private Func<string> _errorText = () => string.Empty;
    private void SetError(Func<string> text) { _errorText = text; Error.Text = text(); }
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        => Error.Text = _errorText();}