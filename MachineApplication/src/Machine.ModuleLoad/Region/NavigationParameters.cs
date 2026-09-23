using System.Collections;
using System.Globalization;

namespace Machine.ModuleLoad.Region;

/// <summary>保留重复查询键及对象引用的参数集合；键名不区分大小写。</summary>
public sealed class NavigationParameters : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly List<KeyValuePair<string, object?>> _values = [];
    public NavigationParameters() { }
    public NavigationParameters(IEnumerable<KeyValuePair<string, object?>> values) => _values.AddRange(values);
    public NavigationParameters(string query) : this(Parse(new Uri("target" + (query.StartsWith('?') ? query : "?" + query), UriKind.Relative))) { }
    public NavigationParameters(Uri uri) : this(Parse(uri)) { }
    public int Count => _values.Count;
    public IEnumerable<string> Keys => _values.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<object?> Values => _values.Select(x => x.Value);
    public object? this[string key] => _values.First(x => KeyEquals(x.Key, key)).Value;
    public NavigationParameters Add(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values.Add(new(key, value));
        return this;
    }

    public bool ContainsKey(string key) => _values.Any(x => KeyEquals(x.Key, key));
    public bool TryGetValue(string key, out object? value)
    {
        foreach (var item in _values)
            if (KeyEquals(item.Key, key)) { value = item.Value; return true; }
        value = null;
        return false;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (!TryGetValue(key, out object? raw)) { value = default; return false; }
        value = ConvertValue<T>(raw);
        return true;
    }

    public T? GetValue<T>(string key) => TryGetValue<T>(key, out var value)
        ? value : throw new KeyNotFoundException($"未找到导航参数 '{key}'。");
    public T? GetValueOrDefault<T>(string key, T? defaultValue = default)
        => TryGetValue<T>(key, out var value) ? value : defaultValue;
    public IEnumerable<T?> GetValues<T>(string key)
        => _values.Where(x => KeyEquals(x.Key, key)).Select(x => ConvertValue<T>(x.Value));
    internal NavigationParameters Copy() => new(this);

    internal static NavigationParameters Merge(Uri uri, NavigationParameters? supplied)
    {
        var merged = Parse(uri);
        if (supplied is null) return merged;
        foreach (var key in supplied.Keys) merged._values.RemoveAll(x => KeyEquals(x.Key, key));
        merged._values.AddRange(supplied);
        return merged;
    }

    public static NavigationParameters Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var text = uri.OriginalString;
        var fragment = text.IndexOf('#');
        if (fragment >= 0) text = text[..fragment];
        var start = text.IndexOf('?');
        var result = new NavigationParameters();
        if (start < 0) return result;
        foreach (var pair in text[(start + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = Decode(equals < 0 ? pair : pair[..equals]);
            if (string.IsNullOrWhiteSpace(key)) continue;
            result.Add(key, equals < 0 ? "" : Decode(pair[(equals + 1)..]));
        }
        return result;
    }

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
    private static bool KeyEquals(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);
    private static T? ConvertValue<T>(object? value)
    {
        if (value is null) return default;
        if (value is T typed) return typed;
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (type.IsEnum) return (T)Enum.Parse(type, Convert.ToString(value, CultureInfo.InvariantCulture)!, true);
        if (type == typeof(Guid)) return (T)(object)Guid.Parse(value.ToString()!);
        return (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => Count == 0 ? "" : "?" + string.Join("&", _values.Select(x =>
        Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(Convert.ToString(x.Value, CultureInfo.InvariantCulture) ?? "")));
}
