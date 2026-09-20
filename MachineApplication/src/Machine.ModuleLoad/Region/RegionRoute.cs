namespace Machine.ModuleLoad.Region;
internal static class RegionRoute
{
    public static string Normalize(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException("区域路由不能为空。", nameof(route));
        return string.Join('/', parts);
    }
}
