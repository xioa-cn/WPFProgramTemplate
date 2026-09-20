namespace Machine.ModuleLoad.Region;
/// <summary>导航上下文。</summary>
public sealed record RegionNavigationContext(string RegionName);
/// <summary>导航感知页面生命周期。</summary>
public interface INavigationAware
{
    bool IsNavigationTarget(RegionNavigationContext context);
    void OnNavigatedTo(RegionNavigationContext context);
    void OnNavigatedFrom();
}
/// <summary>可选的区域激活生命周期。</summary>
public interface IRegionView
{
    void OnActivated();
    void OnDeactivated();
}
