using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

[ModuleDataContext<BtnAuthViewModel>("Common")]
public partial class BtnAuthPage : Page
{
    public BtnAuthPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is BtnAuthViewModel { IsBusy: false, Pages.Count: 0 } model && model.CanBrowse)
                await model.ReloadCommand.ExecuteAsync(null);
        };
    }
}
