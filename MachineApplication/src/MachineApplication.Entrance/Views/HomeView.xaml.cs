using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Mvvm;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

[BtnAuth]
[ModuleDataContext<HomeViewModel>("Common")]
public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
}
