using System.Windows;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[ModuleDataContextAttribute<MainWindowViewModel>("Common")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

}
