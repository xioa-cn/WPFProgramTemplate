using System.Windows;

namespace Machine.ModuleLoad.Region;

/// <summary>导航历史项；视图实例导航只保留弱引用，路由导航可通过 URI 重建。</summary>
public sealed class RegionNavigationJournalEntry : IRegionNavigationJournalEntry
{
    public RegionNavigationJournalEntry(Uri uri, NavigationParameters? parameters = null)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Parameters = parameters is null ? NavigationParameters.Parse(uri) : new(parameters);
    }

    public Uri Uri { get; }
    public NavigationParameters Parameters { get; }
    internal WeakReference<UIElement>? DirectView { get; init; }
    internal Type? DirectViewType { get; init; }
    internal bool KeepAlive { get; init; } = true;
}

/// <summary>成功导航才改变游标；后退后新导航会清除前进历史。</summary>
public sealed class RegionNavigationJournal : IRegionNavigationJournal
{
    private readonly RegionNavigationService _service;
    private readonly List<RegionNavigationJournalEntry> _entries = [];
    private int _index = -1;
    private int _version;
    private INavigateAsync _navigationTarget;

    internal RegionNavigationJournal(RegionNavigationService service)
    {
        _service = service;
        _navigationTarget = service;
    }

    public INavigateAsync NavigationTarget
    {
        get => _navigationTarget;
        set => _navigationTarget = value ?? throw new ArgumentNullException(nameof(value));
    }
    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index + 1 < _entries.Count;
    public IRegionNavigationJournalEntry? CurrentEntry => _index >= 0 ? _entries[_index] : null;
    public IEnumerable<IRegionNavigationJournalEntry> BackStack => _entries.Take(Math.Max(0, _index)).Reverse().ToArray();
    public IEnumerable<IRegionNavigationJournalEntry> ForwardStack => _entries.Skip(_index + 1).ToArray();

    public void GoBack() => Move(false);
    public void GoForward() => Move(true);

    internal bool Move(bool forward)
    {
        if (forward ? !CanGoForward : !CanGoBack) return false;
        var destination = _index + (forward ? 1 : -1);
        var version = _version;
        var entry = _entries[destination];
        bool? result = null;
        void Commit()
        {
            if (version != _version) throw new InvalidOperationException("导航历史已变更。");
            _index = destination;
            _version++;
        }

        if (ReferenceEquals(NavigationTarget, _service))
        {
            _service.Replay(entry, Commit, completed => result = completed.Result);
        }
        else
        {
            // 允许 Prism 风格的 Journal 替换导航目标。替换目标只负责按 URI
            // 导航，游标仍由本 Journal 在目标成功后提交。
            NavigationTarget.RequestNavigate(entry.Uri, completed =>
            {
                if (!completed.Result) { result = false; return; }
                try { Commit(); result = true; }
                catch (Exception) { result = false; }
            }, new NavigationParameters(entry.Parameters));
        }
        return result is not false;
    }

    public void RecordNavigation(IRegionNavigationJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_index + 1 < _entries.Count) _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        var record = entry as RegionNavigationJournalEntry;
        _entries.Add(new RegionNavigationJournalEntry(entry.Uri, entry.Parameters)
        {
            DirectView = record?.DirectView,
            DirectViewType = record?.DirectViewType,
            KeepAlive = record?.KeepAlive ?? true
        });
        _index = _entries.Count - 1;
        _version++;
    }

    public void Clear()
    {
        _service.CancelPending();
        _entries.Clear();
        _index = -1;
        _version++;
    }

    internal void RemoveView(UIElement view, Uri? uri)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            var matches = entry.DirectView?.TryGetTarget(out var target) == true && ReferenceEquals(target, view);
            if (!matches && uri is not null && entry.DirectViewType is null)
                matches = RegionRoute.GetPath(uri).Equals(RegionRoute.GetPath(entry.Uri), StringComparison.OrdinalIgnoreCase);
            if (!matches) continue;
            _entries.RemoveAt(i);
            if (i <= _index) _index--;
        }
        NormalizeCursor();
    }

    internal void RemoveRoute(string path)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].DirectViewType is not null ||
                !RegionRoute.GetPath(_entries[i].Uri).Equals(path, StringComparison.OrdinalIgnoreCase)) continue;
            _entries.RemoveAt(i);
            if (i <= _index) _index--;
        }
        NormalizeCursor();
    }

    private void NormalizeCursor()
    {
        if (_entries.Count == 0) _index = -1;
        else if (_index < 0) _index = 0;
        else if (_index >= _entries.Count) _index = _entries.Count - 1;
        _version++;
    }
}
