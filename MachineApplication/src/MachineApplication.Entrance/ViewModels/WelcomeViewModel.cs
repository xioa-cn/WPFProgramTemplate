using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Region;
using MachineApplication.Entrance.Models;
using MachineApplication.Entrance.Utils;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>欢迎页入口，进入路由配置中的第一个可用页面。</summary>
public partial class WelcomeViewModel(INavigationService navigation) : ObservableObject
{
    [ObservableProperty] private string _status = "";

    /// <summary>按 Router.json 的树顺序进入第一个已注册且当前用户可访问的页面。</summary>
    [RelayCommand]
    private void EnterWorkspace()
    {
        try
        {
            var firstPage = FindPages(RouterConfiguration.Load())
                .FirstOrDefault(page => page.Url is { Length: > 0 } url && navigation.CanNavigate(url));

            if (firstPage?.Url is not { Length: > 0 } target)
                throw new InvalidOperationException("路由配置中没有可访问的页面。");

            navigation.Navigate("MainRegion", target);
            Status = "";
        }
        catch (Exception ex) { Status = ManagementMessages.Error(ex); }
    }

    private static IEnumerable<NavModel> FindPages(IEnumerable<NavModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
            {
                foreach (var page in FindPages(node.Children))
                    yield return page;
            }
            else if (!string.IsNullOrWhiteSpace(node.Url))
            {
                yield return node;
            }
        }
    }
}
