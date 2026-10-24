using System.Windows.Controls;

using System.Windows;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

[ModuleDataContext<ModulesConfigViewModel>("Common")]
public partial class ModulesConfig : UserControl {
    public ModulesConfig() {
        InitializeComponent();
       
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) {
        if (DataContext is ModulesConfigViewModel viewModel) {
            await viewModel.InitializeAsync();
        }
    }
}
