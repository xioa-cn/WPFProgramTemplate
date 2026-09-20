using System.Windows;
using System.Windows.Controls;
namespace MachineApplication.Entrance.Views;
public sealed class HomeView : UserControl
{
    public HomeView() => Content = new TextBlock { Text = "Home view", FontSize = 24, Margin = new Thickness(12) };
}
