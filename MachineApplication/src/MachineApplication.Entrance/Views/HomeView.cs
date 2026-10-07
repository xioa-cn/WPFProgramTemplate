using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.ModuleConfig;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MachineApplication.Entrance.Views;

public sealed class HomeView : UserControl
{
    public HomeView()
    {
        var loadingBar = ModuleProvider.GetModuleProvider("Common")
            .GetRequiredKeyedService<ILoadingBar>("LoadingBar");

        var snackBar = ModuleProvider.GetModuleProvider("Common").GetService<ISnackBar>();
        var title = new TextBlock { FontSize = 24, Margin = new Thickness(12) };
        title.SetBinding(TextBlock.TextProperty, new Binding("MainWindow_Home")
        {
            Source = ViewModelLocator.EntranceLang
        });
        Content = title;

        this.Loaded += (_, _) =>
        {
            loadingBar.LoadingAsync(async () =>
            {
                snackBar?.SendMessage("Hello World!", 3000);
                await Task.Delay(2000);
                GlobalLogger.Warn("WarnNingLogger");
                throw new Exception();
                
            });
        };
    }
}