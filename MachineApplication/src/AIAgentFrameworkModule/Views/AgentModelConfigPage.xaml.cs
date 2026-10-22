using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using Machine.ModuleLoad.Region;
using AIAgentFrameworkModule.ViewModels;
using Machine.ModuleLoad.Logger;

namespace AIAgentFrameworkModule.Views;

/// <summary>模型配置页，只负责 WPF 控件事件与 ViewModel 的桥接。</summary>
public partial class AgentModelConfigPage : Page
{
    private readonly AgentDebugViewModel viewModel;
    private readonly INavigationService navigation;
    private bool synchronizingPassword;

    /// <summary>页面由 DI 注入共享调试 ViewModel，多个路由页看到同一份配置状态。</summary>
    public AgentModelConfigPage(AgentDebugViewModel viewModel, INavigationService navigation)
    {
        this.viewModel = viewModel;
        this.navigation = navigation;
        InitializeComponent();
        DataContext = viewModel;
        GlobalLogger.DebuggerLogger?.Debug("[AI.View] 模型配置页已创建，绑定共享的 AgentDebugViewModel。");
    }

    /// <summary>切换到共享同一配置状态的对话页，不创建新的窗口或 ViewModel。</summary>
    private void OnOpenChat(object sender, RoutedEventArgs args)
    {
        // navigation.RequestNavigate(new Uri("ai/chat", UriKind.Relative));
    }

    /// <summary>只清空内存密钥；PasswordChanged 会同步配置，空值时允许回退环境变量。</summary>
    private void OnClearApiKey(object sender, RoutedEventArgs args) => ApiKeyBox.Clear();

    /// <summary>删除前确认，只有用户同意后才执行删除命令，避免误删。</summary>
    private void OnDeleteProfile(object sender, RoutedEventArgs args)
    {
        // 删除按钮只通过此事件执行命令，避免 Click 与 Command 各执行一次。
        if (!viewModel.DeleteProfileCommand.CanExecute(null)) return;
        if (MessageBox.Show("删除当前模型配置并清空对话？磁盘文件需要点击保存后才会更新。",
            "删除模型", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            viewModel.DeleteProfileCommand.Execute(null);
    }

    /// <summary>窄窗口把模型列表放到表单上方，避免固定侧栏挤压输入框。</summary>
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ProfilesPanel == null || EditorScroll == null) return;
        bool compact = ActualWidth < 780;
        ProfileColumn.Width = new GridLength(compact ? 0 : 235);
        GapColumn.Width = new GridLength(compact ? 0 : 20);
        Grid.SetRow(ProfilesPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(ProfilesPanel, compact ? 3 : 1);
        ProfilesPanel.Height = compact ? 195 : double.NaN;
        ProfilesPanel.Margin = compact ? new Thickness(0, 0, 0, 14) : new Thickness(0);
        Grid.SetColumn(EditorScroll, compact ? 0 : 2);
        Grid.SetColumnSpan(EditorScroll, compact ? 3 : 1);
    }

    /// <summary>WPF PasswordBox 不提供普通双向 Password 绑定，这里只把值写入内存属性。</summary>
    private void OnApiKeyChanged(object sender, RoutedEventArgs args)
    {
        if (!synchronizingPassword && sender is PasswordBox passwordBox && viewModel.SelectedProfile != null)
        {
            viewModel.SelectedProfile.ApiKey = passwordBox.Password;
        }
    }

    /// <summary>页面可见时同步掩码框，防止切换配置后继续显示上一个模型的密钥。</summary>
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        GlobalLogger.DebuggerLogger?.Debug("[AI.View] 模型配置页已加载，同步密钥掩码框。");
        viewModel.PropertyChanged -= OnViewModelChanged;
        viewModel.PropertyChanged += OnViewModelChanged;
        SynchronizePassword();
    }

    /// <summary>只在所选配置变化时同步密码，正常输入时不重复改写控件。</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AgentDebugViewModel.SelectedProfile)) SynchronizePassword();
    }

    /// <summary>设置掩码显示时阻止 PasswordChanged 将旧值写回新的配置。</summary>
    private void SynchronizePassword()
    {
        synchronizingPassword = true;
        try
        {
            ApiKeyBox.Password = viewModel.SelectedProfile?.ApiKey ?? "";
        }
        finally
        {
            synchronizingPassword = false;
        }
    }

    /// <summary>离开页面解除事件订阅；若正在验证连接则取消，空闲会话留给对话页继续使用。</summary>
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        viewModel.PropertyChanged -= OnViewModelChanged;
        GlobalLogger.DebuggerLogger?.Debug($"[AI.View] 模型配置页离开：正在请求={viewModel.IsBusy}。");
        if (viewModel.IsBusy) viewModel.Deactivate();
    }
}
