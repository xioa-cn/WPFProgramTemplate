using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

[ModuleDataContext<SuperViewModel>("Common")]
public partial class SuperPage : Page
{
    public SuperPage()
    {
        InitializeComponent();
    }
}