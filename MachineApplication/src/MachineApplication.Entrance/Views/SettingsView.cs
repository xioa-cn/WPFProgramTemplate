using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Views;

public sealed class SettingsView : UserControl
{
    public SettingsView()
    {
        var title = new TextBlock { FontSize = 24, Margin = new Thickness(12) };
        title.SetBinding(TextBlock.TextProperty, new Binding("MainWindow_Settings")
        {
            Source = ViewModelLocator.EntranceLang
        });
        Content = title;
    }
}