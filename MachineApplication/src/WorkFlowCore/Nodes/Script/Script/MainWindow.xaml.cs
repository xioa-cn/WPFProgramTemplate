using CsxPad.Wpf.Helpers;
using CsxPad.Wpf.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Window = System.Windows.Window;

namespace CsxPad.Wpf;

public partial class MainWindow : Window
{
    private readonly WindowWorkAreaBehavior _workAreaBehavior;

    public MainWindow() : this(new MainWindowViewModel())
    {
    }

    public MainWindow(string script, string title, string? scriptPath = null, string? packageWorkspaceDirectory = null)
        : this(new MainWindowViewModel(nodeScript: script, nodeTitle: title,
            nodeScriptPath: scriptPath, packageWorkspaceDirectory: packageWorkspaceDirectory))
    {
    }

    private MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _workAreaBehavior = WindowWorkAreaBehavior.Attach(this);
        DataContext = viewModel;
        viewModel.ApplyNodeScriptRequested += script =>
        {
            EditedScript = script;
            DialogResult = true;
        };
        Closing += (_, args) =>
        {
            if (!viewModel.IsNodeDocument) return;
            if (EditedScript is null && viewModel.IsDirty && MessageBox.Show(this,
                    "放弃修改并关闭？如需保存，请先点击“应用到节点”。", "CsxPad", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                args.Cancel = true;
                return;
            }
            if (viewModel.StopCommand.CanExecute(null)) viewModel.StopCommand.Execute(null);
        };
    }

    public string? EditedScript { get; private set; }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e) =>
        ShortcutPresentation.ProcessKey(e);

    private void LeftPaneResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        LeftPaneHost.Width = ClampPaneWidth(
            LeftPaneHost,
            LeftPaneHost.ActualWidth + e.HorizontalChange);
    }

    private void RightPaneResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        RightPaneHost.Width = ClampPaneWidth(
            RightPaneHost,
            RightPaneHost.ActualWidth - e.HorizontalChange);
    }

    private static double ClampPaneWidth(FrameworkElement pane, double width) =>
        Math.Clamp(width, pane.MinWidth, pane.MaxWidth);
}
