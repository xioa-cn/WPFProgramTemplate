using System.Windows.Controls;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>欢迎页。通过导航创建时构造函数注入模型，嵌入主窗口时保留无参构造。</summary>
public partial class Welcome : Page
{
    public Welcome() => InitializeComponent();

    public Welcome(WelcomeViewModel viewModel) : this() => DataContext = viewModel;
}
