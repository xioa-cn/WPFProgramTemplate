using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Machine.ModuleLoad.Region;

/// <summary>ContentControl 的单激活区域。集合只能通过区域方法修改。</summary>
public interface IRegion : INavigateAsync
{
    /// <summary>区域注册名。</summary>
    string Name { get; }
    /// <summary>承载区域内容的 ContentControl。</summary>
    ContentControl Host { get; }
    /// <summary>供区域行为或宿主保存的上下文对象。</summary>
    object? Context { get; set; }
    /// <summary>创建此区域的区域管理器。</summary>
    IRegionManager RegionManager { get; }
    /// <summary>此区域专属的导航服务。</summary>
    IRegionNavigationService NavigationService { get; }
    /// <summary>区域中保留的视图。</summary>
    ReadOnlyObservableCollection<UIElement> Views { get; }
    /// <summary>当前活动视图集合；此适配器最多包含一个视图。</summary>
    ReadOnlyObservableCollection<UIElement> ActiveViews { get; }
    /// <summary>添加视图，可选地登记一个区域内名称。</summary>
    void Add(UIElement view, string? viewName = null);
    /// <summary>移除视图并同步清理其导航历史。</summary>
    bool Remove(UIElement view);
    /// <summary>移除区域中的全部视图。</summary>
    void RemoveAll();
    /// <summary>激活已加入区域的视图。</summary>
    void Activate(UIElement view);
    /// <summary>停用当前活动视图。</summary>
    void Deactivate(UIElement view);
    /// <summary>按区域内名称查找视图。</summary>
    UIElement? GetView(string viewName);
    /// <summary>判断视图是否已经加入此区域。</summary>
    bool Contains(UIElement view);
}
