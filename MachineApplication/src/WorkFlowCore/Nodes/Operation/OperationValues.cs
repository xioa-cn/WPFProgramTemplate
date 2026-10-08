using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace WorkFlowCore.Nodes.Operation;

internal static class OperationValues
{
    public static object? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement);
    }

    private static object? Read(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt32(out var integer) => integer,
        JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
        JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.Number => Finite(element.GetDouble()),
        JsonValueKind.Array => element.EnumerateArray().Select(Read).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => Read(property.Value), StringComparer.Ordinal),
        _ => throw new ArgumentException("不支持的 JSON 值。")
    };

    public static bool Boolean(object? value) => value is bool boolean ? boolean
        : throw new ArgumentException("逻辑输入必须为 Boolean，不会隐式转换字符串或数字。");

    public static int Integer(object? value)
    {
        if (value is not (sbyte or byte or short or ushort or int or uint or long or ulong))
            throw new ArgumentException("索引和长度必须为整数。");
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static object?[] Array(object? value)
    {
        if (value is System.Array { Rank: not 1 }) throw new ArgumentException("只支持一维数组。");
        return value is IList list ? list.Cast<object?>().ToArray()
            : throw new ArgumentException("输入必须为一维数组或 IList 列表。");
    }

    public static Dictionary<string, object?> Dictionary(object? value)
    {
        if (value is not IDictionary dictionary) throw new ArgumentException("输入必须为字符串键的字典。");
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key) throw new ArgumentException("字典键必须为字符串。");
            result.Add(key, entry.Value);
        }
        return result;
    }

    public static string Key(object? value) => value as string ?? throw new ArgumentException("字典键必须为字符串，不能为 null。");
    public static bool IsNumber(object? value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    public static bool Equal(object? left, object? right) => IsNumber(left) && IsNumber(right)
        ? Compare(left, right) == 0 : Equals(left, right);

    public static int Compare(object? left, object? right)
    {
        if (IsNumber(left) && IsNumber(right))
        {
            if (left is float or double || right is float or double)
                return Finite(Convert.ToDouble(left, CultureInfo.InvariantCulture))
                    .CompareTo(Finite(Convert.ToDouble(right, CultureInfo.InvariantCulture)));
            return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        }
        if (left is string leftText && right is string rightText) return string.CompareOrdinal(leftText, rightText);
        if (left is DateTime leftDate && right is DateTime rightDate) return leftDate.CompareTo(rightDate);
        throw new ArgumentException("大小比较需要两个数字、两个字符串或两个 DateTime，不能为 null。");
    }

    public static double Finite(double number) => double.IsFinite(number) ? number
        : throw new ArgumentException("不支持 NaN 或无穷大。");

    public static int Index(object? value, int count, bool allowEnd = false)
    {
        var index = Integer(value);
        if (index < 0 || index > count || !allowEnd && index == count)
            throw new ArgumentOutOfRangeException(nameof(value), $"索引 {index} 超出集合范围（共 {count} 项）。");
        return index;
    }
}
