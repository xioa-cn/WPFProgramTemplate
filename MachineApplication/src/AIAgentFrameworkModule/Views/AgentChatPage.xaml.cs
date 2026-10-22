using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AIAgentFrameworkModule.ViewModels;
using AIAgentFrameworkModule.Models;
using AIAgentFrameworkModule.Views.Controls;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Region;

namespace AIAgentFrameworkModule.Views;

/// <summary>对话调试页：绑定 ViewModel，不在页面代码中直接调用模型 SDK。</summary>
public partial class AgentChatPage : Page
{
    private readonly AgentDebugViewModel viewModel;
    private readonly INavigationService navigation;
    private bool followLatest = true;
    private DispatcherOperation? pendingScroll;

    /// <summary>页面由 DI 注入共享的调试状态。</summary>
    public AgentChatPage(AgentDebugViewModel viewModel, INavigationService navigation)
    {
        this.viewModel = viewModel;
        this.navigation = navigation;
        InitializeComponent();
        DataContext = viewModel;
        GlobalLogger.DebuggerLogger?.Debug("[AI.View] 对话调试页已创建，绑定共享的 AgentDebugViewModel。");
        // Popup 独立于页面可视树，直接设置表情列表，避免 AncestorType 绑定跨 Popup 失效。
        EmojiChoicesList.ItemsSource = new[] { "😀", "😄", "😊", "🥰", "🤔", "😎", "😭", "😂", "👍", "👏", "🙏", "💪", "❤️", "🎉", "🚀", "✨", "✅", "⚠️" };
    }

    /// <summary>打开已注册的配置路由，使用宿主默认主区域，不创建另一个调试工作区。</summary>
    private void OnOpenConfiguration(object sender, RoutedEventArgs args)
    {
        // navigation.RequestNavigate(new Uri("ai/models", UriKind.Relative));
    }

    /// <summary>快捷问题只填入输入框，由用户明确点击发送后才产生请求。</summary>
    private void OnSuggestion(object sender, RoutedEventArgs args)
    {
        if (viewModel.IsIdle && sender is Button button && button.Tag is string suggestion)
        {
            viewModel.Prompt = suggestion;
            PromptBox.Focus();
        }
    }

    private void OnPromptPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Return ||
            (Keyboard.Modifiers != ModifierKeys.None && Keyboard.Modifiers != ModifierKeys.Control)) return;

        args.Handled = true;
        if (args.IsRepeat) return;
        PromptBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (viewModel.SendCommand.CanExecute(null)) viewModel.SendCommand.Execute(null);
    }

    /// <summary>在光标处插入完整 Emoji 字符串，替换所选文本而不是覆盖整条问题。</summary>
    private void OnEmojiSelected(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsIdle || sender is not Button button || button.Content is not string emoji) return;
        int position = PromptBox.SelectionStart;
        PromptBox.SelectedText = emoji;
        PromptBox.CaretIndex = position + emoji.Length;
        EmojiToggle.IsChecked = false;
        PromptBox.Focus();
    }

    /// <summary>复制模型返回的原始 Markdown，不复制生成的 UI 或执行代码。</summary>
    private void OnCopyMessage(object sender, RoutedEventArgs args)
    {
        if (sender is Button button && button.DataContext is AgentChatMessage message)
            MarkdownMessageView.CopyText(message.Text, button);
    }

    /// <summary>用户停留在底部时跟随流式输出；向上翻阅时不强行滚回底部。</summary>
    private void OnMessagesScrollChanged(object sender, ScrollChangedEventArgs args)
    {
        // 文本框内部也可能产生滚动事件，只响应外层消息列表的事件。
        if (sender is not ScrollViewer scrollViewer ||
            !ReferenceEquals(args.OriginalSource, scrollViewer) ||
            !ReferenceEquals(scrollViewer.TemplatedParent, MessageList)) return;
        if (args.ExtentHeightChange == 0)
        {
            followLatest = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < 24;
        }
        else if (followLatest && pendingScroll is null)
        {
            pendingScroll = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                pendingScroll = null;
                if (IsLoaded && followLatest) scrollViewer.ScrollToEnd();
            }));
        }
    }

    /// <summary>离开时停止未完成的网络请求；空闲会话允许在配置页与对话页之间复用。</summary>
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        pendingScroll?.Abort();
        pendingScroll = null;
        EmojiToggle.IsChecked = false;
        GlobalLogger.DebuggerLogger?.Debug($"[AI.View] 对话调试页离开：正在请求={viewModel.IsBusy}。");
        if (viewModel.IsBusy) viewModel.Deactivate();
    }
}
