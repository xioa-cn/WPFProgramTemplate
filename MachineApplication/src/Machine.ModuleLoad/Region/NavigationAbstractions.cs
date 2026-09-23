namespace Machine.ModuleLoad.Region;

/// <summary>以完成回调报告成功、取消或异常；确认过程可异步完成。</summary>
public interface INavigateAsync
{
    /// <summary>发起可异步确认和解析的导航。</summary>
    void RequestNavigate(Uri target, Action<NavigationResult>? callback = null,
        NavigationParameters? navigationParameters = null);

    void RequestNavigate(Uri target, NavigationParameters parameters, Action<NavigationResult>? callback = null)
        => RequestNavigate(target, callback, parameters);
}

/// <summary>一次导航的完成结果。取消时 Result=false 且 Error=null。</summary>
public sealed class NavigationResult(RegionNavigationContext? context, bool result, Exception? error = null)
{
    public RegionNavigationContext? Context { get; } = context;
    public bool Result { get; } = result;
    public Exception? Error { get; } = error;
}

public class RegionNavigationEventArgs(RegionNavigationContext context) : EventArgs
{
    public RegionNavigationContext NavigationContext { get; } = context;
    public Uri Uri => NavigationContext.Uri;
}

public sealed class RegionNavigationFailedEventArgs(RegionNavigationContext context, Exception? error)
    : RegionNavigationEventArgs(context)
{
    public Exception? Error { get; } = error;
}

/// <summary>控制视图停用后是否仍保留在区域中；优先检查视图，再检查 DataContext。</summary>
public interface IRegionMemberLifetime
{
    bool KeepAlive { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RegionMemberLifetimeAttribute : Attribute
{
    public bool KeepAlive { get; set; } = true;
}

/// <summary>历史项只保存目标和参数，不强引用视图。</summary>
public interface IRegionNavigationJournalEntry
{
    Uri Uri { get; }
    NavigationParameters Parameters { get; }
}

public interface IRegionNavigationJournal
{
    /// <summary>是否存在可后退的历史项。</summary>
    bool CanGoBack { get; }
    /// <summary>是否存在可前进的历史项。</summary>
    bool CanGoForward { get; }
    /// <summary>当前历史项。</summary>
    IRegionNavigationJournalEntry? CurrentEntry { get; }
    /// <summary>执行历史重放的导航目标。</summary>
    INavigateAsync NavigationTarget { get; set; }
    /// <summary>当前项之前的历史，最近项在前。</summary>
    IEnumerable<IRegionNavigationJournalEntry> BackStack { get; }
    /// <summary>当前项之后的历史，按导航顺序排列。</summary>
    IEnumerable<IRegionNavigationJournalEntry> ForwardStack { get; }
    /// <summary>请求后退；确认或目标导航异步完成后提交游标。</summary>
    void GoBack();
    /// <summary>请求前进；确认或目标导航异步完成后提交游标。</summary>
    void GoForward();
    /// <summary>清除历史并取消区域当前等待的确认。</summary>
    void Clear();
    /// <summary>记录一次成功导航。</summary>
    void RecordNavigation(IRegionNavigationJournalEntry entry);
}
