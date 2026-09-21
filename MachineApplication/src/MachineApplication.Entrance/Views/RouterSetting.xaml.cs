using System.Windows;
using System.Windows.Controls;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

public partial class RouterSetting : Page, INavigationAware
{
    public RouterSetting(RouterSettingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public bool IsNavigationTarget(RegionNavigationContext context) => true;

    /// <summary>页面被缓存复用时重新读取权限等级，避免沿用旧目录。</summary>
    public void OnNavigatedTo(RegionNavigationContext context)
    {
        if (DataContext is RouterSettingViewModel viewModel) viewModel.RefreshLevels();
    }

    public void OnNavigatedFrom() { }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is RouterSettingViewModel viewModel)
            viewModel.SelectedItem = e.NewValue as RouteEditorNode;
    }
}
