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

    public static string GetPath(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var text = uri.IsAbsoluteUri ? uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped) : uri.OriginalString;
        var boundary = text.IndexOfAny(['?', '#']);
        if (boundary >= 0) text = text[..boundary];
        return Normalize(Uri.UnescapeDataString(text));
    }
}
