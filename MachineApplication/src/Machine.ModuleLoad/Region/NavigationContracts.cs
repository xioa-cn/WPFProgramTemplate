namespace Machine.ModuleLoad.Region;

/// <summary>确认导航、复用判断与导航生命周期共用的请求上下文。</summary>
public sealed class RegionNavigationContext
{
    /// <summary>获取本次导航对应的区域导航服务。</summary>
    public IRegionNavigationService NavigationService { get; }
    /// <summary>获取完整导航目标 URI，包含查询字符串。</summary>
    public Uri Uri { get; }
    /// <summary>获取 URI 查询参数和 RequestNavigate 传入的对象参数；同名对象参数覆盖查询参数。</summary>
    public NavigationParameters Parameters { get; }
    /// <summary>获取正在执行导航的真实区域实例。</summary>
    public IRegion Region => NavigationService.Region;
    public string RegionName => Region.Name;

    public RegionNavigationContext(IRegionNavigationService navigationService, Uri uri,
        NavigationParameters? parameters = null)
    {
        NavigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Parameters = NavigationParameters.Merge(uri, parameters);
    }
}

/// <summary>视图与 DataContext 均可实现；同一对象只通知一次。</summary>
public interface INavigationAware
{
    /// <summary>是否复用该实例；返回 false 时由导航服务创建新实例。</summary>
    bool IsNavigationTarget(RegionNavigationContext context);
    void OnNavigatedTo(RegionNavigationContext context);

    /// <summary>兼容原有无参数的离开通知。</summary>
    void OnNavigatedFrom() { }

    /// <summary>在确认通过后通知即将离开，context 描述本次导航的目标。</summary>
    void OnNavigatedFrom(RegionNavigationContext context) => OnNavigatedFrom();
}

public interface IRegionView
{
    void OnActivated();
    void OnDeactivated();
}
