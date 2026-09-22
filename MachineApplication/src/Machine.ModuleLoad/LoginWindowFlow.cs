using System.Windows;
using Machine.ModuleLoad.Mapper;
using Machine.ModuleLoad.ModuleConfig;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad;

/// <summary>统一控制登录、主页显示、账号切换与退出，仅在界面线程使用。</summary>
public sealed class LoginWindowFlow(IServiceProvider services, Application app, PermissionService permissions)
{
    private Window? _main;
    private Window? _login;
    private bool _transitioning;

    /// <summary>准备首次登录窗口；此时不创建主窗口。</summary>
    public Window CreateStartupWindow()
    {
        app.Dispatcher.VerifyAccess();
        if (_login is not null || _main is not null)
            throw new InvalidOperationException("The window flow has already started.");
        var login = CreateLogin(true);
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        app.MainWindow = login;
        return login;
    }

    /// <summary>隐藏现有主页并注销，显示新的居中登录窗口，禁止自动登录旧账号。</summary>
    public void SwitchAccount()
    {
        app.Dispatcher.VerifyAccess();
        if (_main is null || _login is not null || _transitioning) return;
        _main.Hide();
        try
        {
            permissions.Logout();
            var login = CreateLogin(false);
            app.MainWindow = login;
            login.Show();
            login.Activate();
        }
        catch
        {
            // 注销后不能恢复未认证的主页；窗口创建失败则明确退出。
            app.Shutdown();
            throw;
        }
    }

    /// <summary>每次从容器创建新的登录实例，在显示前设置运行模式和事件。</summary>
    private Window CreateLogin(bool allowAutoLogin)
    {
        var login = services.GetRequiredKeyedService<Window>(Config.LoginWindow);
        try
        {
            var contract = (ILoginWindow)login;
            contract.AllowAutoLogin = allowAutoLogin;
            login.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            login.AssemblyUI();
            contract.LoginSucceeded += OnLoginSucceeded;
            login.Closed += OnLoginClosed;
            _login = login;
            return login;
        }
        catch
        {
            login.Close();
            throw;
        }
    }

    /// <summary>认证成功后显示或恢复主页，成功转交主窗口引用后才关闭登录窗口。</summary>
    private void OnLoginSucceeded(object? sender, EventArgs args)
    {
        app.Dispatcher.VerifyAccess();
        if (_transitioning || _login is null || !ReferenceEquals(sender, _login)) return;
        if (permissions.CurrentUser is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        _transitioning = true;
        var login = _login;
        Window? candidate = _main;
        var created = candidate is null;
        try
        {
            if (created)
            {
                candidate = services.GetRequiredKeyedService<Window>(Config.StartupWindow);
                candidate.AssemblyUI();
            }
            app.MainWindow = candidate!;
            candidate!.Show();
            candidate.Activate();
            _main = candidate;
        }
        catch
        {
            // 登录窗口仍存活：恢复其退出职责，丢弃失败的新窗口，允许再次认证。
            app.MainWindow = login;
            if (created) candidate?.Close();
            else candidate?.Hide();
            permissions.Logout();
            throw;
        }
        finally
        {
            _transitioning = false;
        }

        _login = null;
        login.Close();
    }

    /// <summary>解除事件订阅；未成功转入主页便关闭登录窗口时退出整个应用。</summary>
    private void OnLoginClosed(object? sender, EventArgs args)
    {
        if (sender is not Window login) return;
        ((ILoginWindow)login).LoginSucceeded -= OnLoginSucceeded;
        login.Closed -= OnLoginClosed;
        if (!ReferenceEquals(_login, login)) return;
        _login = null;
        app.Shutdown();
    }
}
