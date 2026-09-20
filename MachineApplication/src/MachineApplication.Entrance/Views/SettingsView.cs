using System.Windows;
using System.Windows.Controls;
namespace MachineApplication.Entrance.Views;
public sealed class SettingsView : UserControl
{
    public SettingsView() => Content = new TextBlock { Text = "Settings view", FontSize = 24, Margin = new Thickness(12) };
}
