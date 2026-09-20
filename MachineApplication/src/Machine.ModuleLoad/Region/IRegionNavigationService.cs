using System.Windows;
namespace Machine.ModuleLoad.Region;
/// <summary>区域导航服务。</summary>
public interface IRegionNavigationService
{
    UIElement Navigate(string regionName, UIElement view, bool keepAlive = true);
    bool CanNavigate(string regionName);
    bool GoBack(string regionName);
}
