using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

[ModuleDataContext<HomeViewModel>("Common")]
public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
}
