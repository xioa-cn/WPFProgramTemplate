using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AIAgentFrameworkModule.ViewModels;
using Machine.ModuleLoad.Region;

namespace AIAgentFrameworkModule.Views;

public partial class AgentGroupChatPage : Page
{
    public static readonly DependencyProperty MessageBubbleMaxWidthProperty = DependencyProperty.Register(
        nameof(MessageBubbleMaxWidth), typeof(double), typeof(AgentGroupChatPage), new PropertyMetadata(560d));

    public double MessageBubbleMaxWidth
    {
        get => (double)GetValue(MessageBubbleMaxWidthProperty);
        private set => SetValue(MessageBubbleMaxWidthProperty, value);
    }

    private readonly AgentGroupChatViewModel viewModel;
    private readonly INavigationService navigation;
    private bool followLatest = true;
    private DispatcherOperation? pendingScroll;

    public AgentGroupChatPage(AgentGroupChatViewModel viewModel, INavigationService navigation)
    {
        this.viewModel = viewModel;
        this.navigation = navigation;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.RefreshProfiles();
        UpdateChatLayout();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        pendingScroll?.Abort();
        pendingScroll = null;
        viewModel.Cancel();
    }

    private void OnConfigure(object sender, RoutedEventArgs args) =>
        navigation.RequestNavigate("MainRegion", new Uri("ai/group-chat/settings", UriKind.Relative));

    private void OnExample(object sender, RoutedEventArgs args)
    {
        viewModel.Draft = "大家先介绍一下自己吧，顺便聊聊今天最想做的一件事。";
        MessageInput.Focus();
    }

    private void OnClear(object sender, RoutedEventArgs args)
    {
        if (MessageBox.Show("清空本次群聊记录？群友配置不会被删除。", "清空聊天", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes && viewModel.ClearCommand.CanExecute(null))
            viewModel.ClearCommand.Execute(null);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Control) return;
        args.Handled = true;
        if (viewModel.SendCommand.CanExecute(null)) viewModel.SendCommand.Execute(null);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs args)
    {
        if (!ReferenceEquals(args.OriginalSource, ChatScroll)) return;
        if (args.ExtentHeightChange == 0 && args.VerticalChange != 0)
            followLatest = ChatScroll.ScrollableHeight - ChatScroll.VerticalOffset < 32;
        if (args.ExtentHeightChange == 0 || !followLatest || pendingScroll is not null) return;
        pendingScroll = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            pendingScroll = null;
            if (IsLoaded && followLatest) ChatScroll.ScrollToEnd();
        }));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateChatLayout();
    }

    private void UpdateChatLayout()
    {
        if (MembersPanel is null) return;
        bool compact = ActualWidth < 1040;
        MembersPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MembersColumn.Width = new GridLength(compact ? 0 : 264);
        double conversationWidth = Math.Max(0, ActualWidth - (compact ? 0 : 264));
        MessageBubbleMaxWidth = Math.Clamp((Math.Min(conversationWidth, 1176) - 160) * 0.75, 80, 560);
        SendHint.Visibility = conversationWidth < 660 ? Visibility.Collapsed : Visibility.Visible;
    }
}
