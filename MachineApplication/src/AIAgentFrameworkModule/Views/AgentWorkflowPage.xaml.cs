using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Threading;
using Microsoft.Win32;
using AIAgentFrameworkModule.ViewModels;
using AIAgentFrameworkModule.Views.Controls;

namespace AIAgentFrameworkModule.Views;

public partial class AgentWorkflowPage : Page
{
    private readonly AgentWorkflowViewModel viewModel;
    private bool followLatest = true;
    private DispatcherOperation? pendingScroll;

    public AgentWorkflowPage(AgentWorkflowViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.RefreshProfiles();
        viewModel.PropertyChanged -= OnViewModelChanged;
        viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        viewModel.PropertyChanged -= OnViewModelChanged;
        pendingScroll?.Abort();
        pendingScroll = null;
        viewModel.Cancel();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AgentWorkflowViewModel.IsBusy) && viewModel.IsBusy)
            WorkspaceTabs.SelectedIndex = 1;
        if (args.PropertyName == nameof(AgentWorkflowViewModel.HasPendingCommand) && viewModel.HasPendingCommand)
            WorkspaceTabs.SelectedIndex = 1;
        if (args.PropertyName != nameof(AgentWorkflowViewModel.SelectedStep)) return;
        pendingScroll?.Abort();
        pendingScroll = null;
        followLatest = true;
        OutputScroll.ScrollToTop();
    }

    private void OnOutputScrollChanged(object sender, ScrollChangedEventArgs args)
    {
        if (!ReferenceEquals(args.OriginalSource, OutputScroll)) return;
        if (args.ExtentHeightChange == 0 && args.VerticalChange != 0)
            followLatest = OutputScroll.ScrollableHeight - OutputScroll.VerticalOffset < 24;
        if (args.ExtentHeightChange == 0 || !followLatest || pendingScroll is not null || viewModel.SelectedStep?.IsGenerating != true) return;
        pendingScroll = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            pendingScroll = null;
            if (IsLoaded && followLatest && viewModel.SelectedStep?.IsGenerating == true) OutputScroll.ScrollToEnd();
        }));
    }

    private void OnUseExample(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsIdle) return;
        viewModel.TaskText = "设计一个 C# 设备数据采集模块，考虑重试、取消、异常恢复和测试。请给出方案、风险分析与验收建议。";
        TaskInput.BringIntoView();
        TaskInput.Focus();
    }

    private void OnCopyStep(object sender, RoutedEventArgs args)
    {
        if (sender is Button button && viewModel.SelectedStep is { Output.Length: > 0 } step)
            MarkdownMessageView.CopyText(step.Output, button);
    }

    private void OnSelectWorkspace(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsIdle) return;
        OpenFolderDialog dialog = new() { Title = "选择允许写入的项目目录", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        try { viewModel.SelectWorkspace(dialog.FolderName); }
        catch (Exception) { MessageBox.Show("请选择已存在的本机项目目录，不能选择磁盘根目录或符号链接。", "目录未授权"); }
    }

    private void OnNewTask(object sender, RoutedEventArgs args)
    {
        if (!viewModel.NewTaskCommand.CanExecute(null)) return;
        if (MessageBox.Show("放弃当前续跑记录，改为编辑新任务？已完成步骤将不再用于续跑。", "开始新任务", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            viewModel.NewTaskCommand.Execute(null);
    }

    private async void OnPreviewFiles(object sender, RoutedEventArgs args)
    {
        if (!viewModel.CanPreviewFiles || viewModel.FilePlan is not { } plan) return;
        AgentFilePreviewWindow preview = new(plan) { Owner = Window.GetWindow(this) };
        if (preview.ShowDialog() == true) await viewModel.ApplyFilesAsync(plan);
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ConfigurationPanel is null || ResultsPanel is null) return;
        bool compact = ActualWidth < 900;
        Workspace.Margin = compact ? new Thickness(12) : new Thickness(20, 16, 20, 16);
        HeaderBadge.Visibility = ActualWidth < 720 ? Visibility.Collapsed : Visibility.Visible;
        ConfigurationColumn.Width = new GridLength(compact ? 0 : 320);
        GapColumn.Width = new GridLength(compact ? 0 : 14);
        Grid.SetRow(ConfigurationPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(ConfigurationPanel, compact ? 3 : 1);
        ConfigurationPanel.MaxHeight = compact ? Math.Clamp(ActualHeight * 0.25, 130, 220) : double.PositiveInfinity;
        ConfigurationPanel.Margin = new Thickness(0, 0, 0, compact ? 12 : 0);
        Grid.SetColumn(ResultsPanel, compact ? 0 : 2);
        Grid.SetColumnSpan(ResultsPanel, compact ? 3 : 1);
        bool narrow = ActualWidth < 720;
        ActionsColumn.Width = narrow ? new GridLength(0) : GridLength.Auto;
        Grid.SetColumnSpan(ConsentPanel, narrow ? 2 : 1);
        Grid.SetRow(ActionsPanel, narrow ? 1 : 0);
        Grid.SetColumn(ActionsPanel, narrow ? 0 : 1);
        Grid.SetColumnSpan(ActionsPanel, narrow ? 2 : 1);
        ActionsPanel.Margin = new Thickness(0, narrow ? 10 : 0, 0, 0);
    }
}
