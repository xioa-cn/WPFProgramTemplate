using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RustSharp;

namespace Machine.ModuleLoad.Utils;

public static class JsonUtils {
    private static readonly Lazy<JsonSerializerOptions> _options = new(() => new JsonSerializerOptions {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    });

    private static JsonSerializerOptions? _userOptions;

    private static readonly object _lock = new();

    /// <summary>
    /// 设置全局自定义Json配置，内部会自动拷贝，不持有外部传入实例引用
    /// 传 null 则切回系统默认
    /// </summary>
    public static void SetJsonUtilsOptions(JsonSerializerOptions? options) {
        lock (_lock) {
            if (options is null) {
                _userOptions = null;
                return;
            }

            // 从传入的options克隆一份，副本是可修改的（还没被序列化冻结）
            _userOptions = new JsonSerializerOptions(options);
        }
    }

    public static JsonSerializerOptions GetNormalSystemOption() {
        return new JsonSerializerOptions(_options.Value);
    }

    internal static JsonSerializerOptions Options {
        get {
            if (_userOptions == null) {
                return _options.Value;
            }

            return _userOptions;
        }
    }

    public static Result<string, Exception> ToJson<T>(this T obj, JsonSerializerOptions? options = null)
        where T : notnull {
        try {
            ArgumentNullException.ThrowIfNull(obj);
            options ??= Options;
            return Result<string, Exception>.Ok(JsonSerializer.Serialize(obj, options));
        }
        catch (Exception exception) {
            return Result<string, Exception>.Err(exception);
        }
    }

    public static Result<T, Exception> JsonToModel<T>(this string jsonStr, JsonSerializerOptions? options = null)
        where T : notnull {
        try {
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonStr);
            options ??= Options;
            var value = JsonSerializer.Deserialize<T>(jsonStr.TrimStart('\uFEFF'), options);
            return value is null
                ? Result<T, Exception>.Err(new InvalidOperationException("JSON contains null."))
                : Result<T, Exception>.Ok(value);
        }
        catch (Exception exception) {
            return Result<T, Exception>.Err(exception);
        }
    }
}