using System.Windows;
using System.Windows.Controls;

namespace Machine.ModuleLoad.Region;

public interface IRegionManager
{
    /// <summary>当前视图解析使用的模块服务容器名称；根容器为 null。</summary>
    string? ServiceContainerName { get; }
    /// <summary>获取此管理器当前注册的实时区域集合。</summary>
    IRegionCollection Regions { get; }
    /// <summary>将 ContentControl 注册为区域。</summary>
    void Register(string regionName, ContentControl host);
    /// <summary>获取已注册区域。</summary>
    IRegion GetRegion(string regionName);
    /// <summary>向区域添加视图。</summary>
    IRegionManager AddToRegion(string regionName, UIElement view);
    /// <summary>按类型注册区域发现视图。</summary>
    IRegionManager RegisterViewWithRegion(string regionName, Type viewType);
    /// <summary>按工厂注册区域发现视图。</summary>
    IRegionManager RegisterViewWithRegion(string regionName, Func<UIElement> viewFactory);
    /// <summary>创建使用独立 DI Scope、区域集合和视图发现注册的子区域管理器。</summary>
    IRegionManager CreateRegionManager();
    /// <summary>创建绑定到指定模块服务容器的子区域管理器。</summary>
    IRegionManager CreateRegionManager(string serviceContainerName);
    /// <summary>在区域中按 URI 发起导航。</summary>
    void RequestNavigate(string regionName, Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null);
    void RequestNavigate(string regionName, Uri target, NavigationParameters parameters,
        Action<NavigationResult>? callback = null);
}

/// <summary>实时区域集合；枚举顺序为注册顺序。</summary>
public interface IRegionCollection : IEnumerable<IRegion>
{
    int Count { get; }
    IRegion this[string regionName] { get; }
    bool ContainsRegionWithName(string regionName);
    bool Remove(string regionName);
}
