using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>欢迎页，界面数据由模块容器统一组装。</summary>
[ModuleDataContext<WelcomeViewModel>("Common")]
public partial class Welcome : Page
{
    public Welcome() => InitializeComponent();
}
