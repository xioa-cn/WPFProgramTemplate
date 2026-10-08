using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CsxPad.Wpf.Models;
using CsxPad.Wpf.ViewModels;

namespace CsxPad.Wpf.Controls;

public partial class WorkspaceExplorer : UserControl
{
    public WorkspaceExplorer() => InitializeComponent();

    private void TreeItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: ScriptWorkspaceItem { IsFolder: false } item } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OpenWorkspaceItemCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void WorkspaceTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (WorkspaceTree.SelectedItem is not ScriptWorkspaceItem item ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.F2 => viewModel.RenameWorkspaceItemCommand,
            Key.Delete => viewModel.DeleteWorkspaceItemCommand,
            _ => null
        };
        if (command?.CanExecute(item) != true)
        {
            return;
        }

        command.Execute(item);
        e.Handled = true;
    }

    private void WorkspaceTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
