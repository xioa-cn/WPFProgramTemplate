using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using WorkFlowCore.Nodes.Operation;

namespace WorkFlowCore.Nodes.NodeSystem;

internal static class SystemValues
{
    public static string Text(object? value) => value as string ?? throw new ArgumentException("输入必须是字符串，不能为 null。");

    public static string FullPath(object? value)
    {
        var text = Text(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return Path.GetFullPath(text, AppContext.BaseDirectory);
    }

    public static void CheckDeletePath(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        if (string.Equals(trimmed, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("不允许删除磁盘根目录。");
    }

    public static Encoding Encoding(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => new UTF8Encoding(false, true),
        TextEncodingKind.Utf8Bom => new UTF8Encoding(true, true),
        TextEncodingKind.Unicode => new UnicodeEncoding(false, true, true),
        TextEncodingKind.BigEndianUnicode => new UnicodeEncoding(true, true, true),
        TextEncodingKind.Ascii => System.Text.Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static DateTime Date(object? value) => value switch
    {
        DateTime date => date,
        string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        _ => throw new ArgumentException("输入必须为 DateTime 或日期字符串。")
    };

    public static byte[] Bytes(object? value) => value switch
    {
        byte[] bytes => bytes,
        string json => ByteArrayFromJson(json),
        _ => throw new ArgumentException("输入必须为 byte[] 或 JSON 字节数组，例如 [0,127,255]。")
    };

    private static byte[] ByteArrayFromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new ArgumentException("需要 JSON 字节数组。");
        return document.RootElement.EnumerateArray().Select(value => value.GetByte()).ToArray();
    }

    public static object? JsonValue(string json) => OperationValues.Parse(json);
}

public enum TextEncodingKind { Utf8, Utf8Bom, Unicode, BigEndianUnicode, Ascii }
