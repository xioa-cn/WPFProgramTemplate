namespace WorkFlowCore.Nodes.Data;

/// <summary>工作流进程级全局数据存储；键不区分大小写，允许显式保存 null，重启后不保留。</summary>
public static class GlobalDataStore
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, object?> Values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>写入或覆盖全局数据。</summary>
    public static void Set(string key, object? value)
    {
        var normalizedKey = NormalizeKey(key);
        lock (SyncRoot) Values[normalizedKey] = value;
    }

    /// <summary>读取全局数据；不存在的键返回 false。</summary>
    public static bool TryGet(string key, out object? value)
    {
        var normalizedKey = NormalizeKey(key);
        lock (SyncRoot) return Values.TryGetValue(normalizedKey, out value);
    }

    /// <summary>删除指定全局数据，供后续流程管理功能复用。</summary>
    public static bool Remove(string key)
    {
        var normalizedKey = NormalizeKey(key);
        lock (SyncRoot) return Values.Remove(normalizedKey);
    }

    /// <summary>清空当前进程内所有全局数据。</summary>
    public static void Clear()
    {
        lock (SyncRoot) Values.Clear();
    }

    /// <summary>统一校验并规范化全局数据键。</summary>
    private static string NormalizeKey(string key)
    {
        var normalizedKey = (key ?? string.Empty).Trim();
        if (normalizedKey.Length == 0) throw new ArgumentException("全局数据键不能为空。", nameof(key));
        return normalizedKey;
    }
}
