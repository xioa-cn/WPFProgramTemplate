using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>色盘页面，数据上下文通过导航模块容器构造函数注入。</summary>
public partial class ThemeColorsView : UserControl
{
    public ThemeColorsView(ThemeColorsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
