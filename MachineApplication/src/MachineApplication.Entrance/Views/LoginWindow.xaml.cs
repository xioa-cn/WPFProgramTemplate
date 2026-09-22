using Machine.ModuleLoad;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using Machine.ModuleLoad.Mapper;
using MachineApplication.Entrance.ViewModels;
using MachineApplication.Entrance.Utils;

namespace MachineApplication.Entrance.Views;

/// <summary>登录窗口，负责交互动画、一次性密码传递及登录偏好。</summary>
public partial class LoginWindow : Window, ILoginWindow
{
    private readonly PermissionService _permissions;
    /// <summary>由框架在显示前设置，切换账号时禁用自动登录。</summary>
    public bool AllowAutoLogin { get; set; } = true;
    /// <summary>认证通过后交由框架切换窗口。</summary>
    public event EventHandler? LoginSucceeded;
    private readonly CancellationTokenSource _closing = new();
    private bool _busy;
    private bool _restoring;
    private Func<string> _errorText = () => "";

    public LoginWindow(PermissionService permissions)
    {
        InitializeComponent();
        _permissions = permissions;

        System.ComponentModel.PropertyChangedEventManager.AddHandler(ViewModelLocator.EntranceLang, OnLanguageChanged, string.Empty);
        Loaded += OnLoaded;
        Closed += (_, _) => { _closing.Cancel(); Password.Clear(); };
    }

    /// <summary>恢复偏好；切换账号时不执行自动登录。</summary>
    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoaded;
        _restoring = true;
        var settings = LoginPreferences.Load();
        Account.Text = settings.Account;
        Password.Password = settings.ReadPassword();
        Remember.IsChecked = Password.Password.Length > 0;
        Automatic.IsChecked = Remember.IsChecked == true && settings.AutoLogin;
        _restoring = false;
        Account.Focus();
        if (AllowAutoLogin && Automatic.IsChecked == true) await SignInAsync();
    }

    private async void LoginClick(object sender, RoutedEventArgs args) => await SignInAsync();

    /// <summary>防止重复提交，异步校验并在完成后恢复交互。</summary>
    private async Task SignInAsync()
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(Account.Text) || Password.Password.Length == 0)
        { SetError(() => ViewModelLocator.EntranceLang.Login_Required); return; }
        _busy = true;
        Form.IsEnabled = false;
        Busy.Visibility = Visibility.Visible;
        Submit.Visibility = Visibility.Collapsed;
        SetError(() => "");
        var password = Password.Password;
        var account = Account.Text.Trim();
        try
        {
            if (!await _permissions.LoginAsync(account, password, _closing.Token))
            {
                Automatic.IsChecked = false;
                LoginPreferences.Save(account, "", false, false);
                Password.Clear();
                SetError(() => ViewModelLocator.EntranceLang.Management_InvalidLogin);
                return;
            }
            try { LoginPreferences.Save(account, password, Remember.IsChecked == true, Automatic.IsChecked == true); }
            catch
            {
                _permissions.Logout();
                SetError(() => ViewModelLocator.EntranceLang.Login_SaveFailed);
                return;
            }
            CompleteLogin();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetError(() => ViewModelLocator.EntranceLang.Management_LoginFailed + ManagementMessages.Error(ex)); }
        finally
        {
            password = "";
            _busy = false;
            Form.IsEnabled = true;
            Busy.Visibility = Visibility.Collapsed;
            Submit.Visibility = Visibility.Visible;
        }
    }

    /// <summary>仅报告认证结果，窗口创建、恢复和退出由框架统一控制。</summary>
    private void CompleteLogin()
    {
        var handler = LoginSucceeded
            ?? throw new InvalidOperationException("The login window is not attached to a window flow.");
        handler(this, EventArgs.Empty);
    }
    /// <summary>取消记住密码时立即清除磁盘凭据，自动登录依赖记住密码。</summary>
    private void PreferencesChanged(object sender, RoutedEventArgs args)
    {
        if (_restoring || _busy) return;
        if (ReferenceEquals(sender, Automatic) && Automatic.IsChecked == true) Remember.IsChecked = true;
        if (Remember.IsChecked != true) Automatic.IsChecked = false;
        try
        {
            if (Remember.IsChecked != true) LoginPreferences.Save(Account.Text.Trim(), "", false, false);
            else if (Automatic.IsChecked != true)
            {
                var saved = LoginPreferences.Load();
                LoginPreferences.Save(saved.Account, saved.ReadPassword(), true, false);
            }
        }
        catch { SetError(() => ViewModelLocator.EntranceLang.Login_SaveFailed); }
    }

    /// <summary>关闭窗口，已有 Closed 回调负责取消未完成的登录验证。</summary>
    private void CloseClick(object sender, RoutedEventArgs args)
    {
        args.Handled = true;
        Close();
    }

    /// <summary>允许卡片标题和空白处拖动，输入控件与按钮保留自身鼠标操作。</summary>
    private void DragWindow(object sender, MouseButtonEventArgs args)
    {
        if (args.ChangedButton != MouseButton.Left || args.LeftButton != MouseButtonState.Pressed) return;
        var source = args.OriginalSource as DependencyObject;
        while (source is not null && source != this)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.Primitives.Selector)
                return;

            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : source is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(source);
        }

        args.Handled = true;
        DragMove();
    }
    private void SetError(Func<string> text) { _errorText = text; Error.Text = text(); }
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Error.Text = _errorText();
}
