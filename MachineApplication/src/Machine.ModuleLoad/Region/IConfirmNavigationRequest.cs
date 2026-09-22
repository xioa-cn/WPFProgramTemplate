namespace Machine.ModuleLoad.Region;

/// <summary>
/// 在当前页面离开前确认是否允许继续导航。
/// 典型场景是编辑页面存在未保存内容时，由页面或 ViewModel 显示确认对话框并返回选择。
/// </summary>
public interface IConfirmNavigationRequest : INavigationAware
{
    /// <summary>请求当前页面确认本次导航是否可以继续。</summary>
    /// <param name="navigationContext">包含目标区域信息的导航上下文。</param>
    /// <param name="continuationCallback">传入 <see langword="true" /> 允许导航，传入 <see langword="false" /> 取消导航；确认流程必须调用一次且只能调用一次。</param>
    void ConfirmNavigationRequest(RegionNavigationContext navigationContext, Action<bool> continuationCallback);
}
