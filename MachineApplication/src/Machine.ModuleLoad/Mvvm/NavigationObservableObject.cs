using CommunityToolkit.Mvvm.ComponentModel;
using Machine.ModuleLoad.Region;

namespace Machine.ModuleLoad.Mvvm;

public abstract class NavigationObservableObject : ObservableObject, INavigationAware
{
    public virtual bool IsNavigationTarget(RegionNavigationContext context)
    {
        return true;
    }

    public virtual void OnNavigatedTo(RegionNavigationContext context)
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }
}