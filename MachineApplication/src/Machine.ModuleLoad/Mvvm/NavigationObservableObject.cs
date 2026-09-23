using CommunityToolkit.Mvvm.ComponentModel;
using Machine.ModuleLoad.Region;

namespace Machine.ModuleLoad.Mvvm;

public abstract class NavigationObservableObject : ObservableObject, IConfirmNavigationRequest
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

    public virtual void OnNavigatedFrom(RegionNavigationContext context) => OnNavigatedFrom();

    public virtual void ConfirmNavigationRequest(
        RegionNavigationContext navigationContext,
        Action<bool> continuationCallback)
    {
        ArgumentNullException.ThrowIfNull(continuationCallback);
        continuationCallback(true);
    }
}
