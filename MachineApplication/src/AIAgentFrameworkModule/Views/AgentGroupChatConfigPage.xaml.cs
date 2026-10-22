using System.Windows;
using System.Windows.Controls;
using AIAgentFrameworkModule.ViewModels;
using Machine.ModuleLoad.Region;

namespace AIAgentFrameworkModule.Views;

public partial class AgentGroupChatConfigPage : Page
{
    private readonly AgentGroupChatViewModel viewModel;
    private readonly INavigationService navigation;
    public AgentGroupChatConfigPage(AgentGroupChatViewModel viewModel, INavigationService navigation)
    {
        this.viewModel = viewModel;
        this.navigation = navigation;
        InitializeComponent();
        DataContext = viewModel;
    }
    private void OnLoaded(object sender, RoutedEventArgs args) => viewModel.RefreshProfiles();
    private void OnChat(object sender, RoutedEventArgs args) =>
        navigation.RequestNavigate("MainRegion", new Uri("ai/workflow", UriKind.Relative));
    private void OnModels(object sender, RoutedEventArgs args) =>
        navigation.RequestNavigate("MainRegion", new Uri("ai/models", UriKind.Relative));
}
