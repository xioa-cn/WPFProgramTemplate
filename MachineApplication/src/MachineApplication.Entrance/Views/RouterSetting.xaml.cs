using System.Windows;
using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

public partial class RouterSetting : Page
{
    public RouterSetting(RouterSettingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is RouterSettingViewModel viewModel)
            viewModel.SelectedItem = e.NewValue as RouteEditorNode;
    }
}
