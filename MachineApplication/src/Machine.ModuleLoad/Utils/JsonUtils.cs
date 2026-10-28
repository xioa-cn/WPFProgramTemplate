using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RustSharp;

namespace Machine.ModuleLoad.Utils;

public static class JsonUtils
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Result<string, Exception> ToJson<T>(this T obj) where T : notnull
    {
        try
        {
            ArgumentNullException.ThrowIfNull(obj);
            return Result<string, Exception>.Ok(JsonSerializer.Serialize(obj, Options));
        }
        catch (Exception exception)
        {
            return Result<string, Exception>.Err(exception);
        }
    }

    public static Result<T, Exception> ToModel<T>(this string jsonStr) where T : notnull
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonStr);
            var value = JsonSerializer.Deserialize<T>(jsonStr.TrimStart('\uFEFF'), Options);
            return value is null
                ? Result<T, Exception>.Err(new InvalidOperationException("JSON contains null."))
                : Result<T, Exception>.Ok(value);
        }
        catch (Exception exception)
        {
            return Result<T, Exception>.Err(exception);
        }
    }
}
