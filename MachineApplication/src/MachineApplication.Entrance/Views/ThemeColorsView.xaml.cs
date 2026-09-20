using System.Windows.Controls;
using Machine.ModuleLoad.ModuleConfig;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

/// <summary>色盘页面，数据上下文由模块 UI 组装器从 Common 子容器解析。</summary>
[ModuleDataContextAttribute<ThemeColorsViewModel>("Common")]
public partial class ThemeColorsView : UserControl
{
    public ThemeColorsView() => InitializeComponent();
}
