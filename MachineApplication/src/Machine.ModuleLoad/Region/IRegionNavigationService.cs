namespace Machine.ModuleLoad.Region;

/// <summary>绑定到一个区域的导航服务；每个区域拥有独立的 Journal。</summary>
public interface IRegionNavigationService : INavigateAsync
{
    /// <summary>获取或设置此服务当前绑定的区域。</summary>
    IRegion Region { get; set; }
    /// <summary>获取此区域的导航历史。</summary>
    IRegionNavigationJournal Journal { get; }
    /// <summary>获取当前成功导航的 URI。</summary>
    Uri? CurrentSource { get; }
    /// <summary>Prism 兼容别名，获取当前成功导航的 URI。</summary>
    Uri? CurrentUri { get; }
    /// <summary>导航开始切换视图前发生。</summary>
    event EventHandler<RegionNavigationEventArgs>? Navigating;
    /// <summary>导航成功完成后发生。</summary>
    event EventHandler<RegionNavigationEventArgs>? Navigated;
    /// <summary>解析、确认或生命周期处理失败时发生。</summary>
    event EventHandler<RegionNavigationFailedEventArgs>? NavigationFailed;
}
