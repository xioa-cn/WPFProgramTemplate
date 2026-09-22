namespace Machine.ModuleLoad.Region;
/// <summary>导航上下文。</summary>
public sealed record RegionNavigationContext(string RegionName);
/// <summary>
/// 导航感知生命周期。该接口既可由视图实现，也可由视图的 DataContext（通常是 ViewModel）实现。
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// 判断当前对象是否可以复用来承载本次导航。
    /// </summary>
    /// <param name="context">包含目标区域名称的导航上下文。</param>
    /// <returns>返回 <see langword="true" /> 表示允许继续导航；返回 <see langword="false" /> 表示拒绝本次导航。</returns>
    bool IsNavigationTarget(RegionNavigationContext context);

    /// <summary>
    /// 页面或 ViewModel 成为目标区域的当前内容后调用。
    /// </summary>
    /// <param name="context">包含目标区域名称的导航上下文。</param>
    void OnNavigatedTo(RegionNavigationContext context);

    /// <summary>
    /// 页面或 ViewModel 即将离开当前区域时调用，可用于停止任务、取消订阅或保存临时状态。
    /// </summary>
    void OnNavigatedFrom();
}
/// <summary>可选的区域激活生命周期。</summary>
public interface IRegionView
{
    void OnActivated();
    void OnDeactivated();
}
