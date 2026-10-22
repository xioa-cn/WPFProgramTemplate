using System.Windows;
using AIAgentFrameworkModule.Services;

namespace AIAgentFrameworkModule.Views;

public partial class AgentFilePreviewWindow : Window
{
    public AgentFilePreviewWindow(AgentFilePlan plan)
    {
        InitializeComponent();
        DataContext = plan;
    }
    private void OnConfirm(object sender, RoutedEventArgs args)
    {
        if (Approval.IsChecked == true) DialogResult = true;
    }
}
