using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Machine.ModuleLoad.Logger;
using RestSharp;

namespace Machine.ModuleLoad.Utils;

/// <summary>通用 UTF-8 JSON 文件读写；预期失败通过 Result 返回，不要求调用方捕获异常。</summary>
public static class JsonFileUtils
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>读取 JSON；可提供文件不存在时的默认值工厂，文件损坏不会使用该工厂掩盖错误。</summary>
    /// <typeparam name="T">需要反序列化的配置类型。</typeparam>
    /// <param name="filePath">JSON 文件路径。</param>
    /// <param name="whenMissing">仅在文件不存在时调用；省略则返回错误结果。</param>
    /// <returns>配置对象或英文错误信息。</returns>
    public static Result<T, string> Read<T>(string filePath, Func<T>? whenMissing = null) where T : notnull
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<T, string>.Err("JSON file path cannot be empty.");
        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(filePath, Encoding.UTF8), Options);
            return value is null
                ? Result<T, string>.Err($"JSON file '{filePath}' contains null.")
                : Result<T, string>.Ok(value);
        }
        catch (FileNotFoundException) when (whenMissing is not null)
        {
            return Result<T, string>.Ok(whenMissing());
        }
        catch (Exception exception)
        {
            var error = $"Failed to read JSON file '{filePath}': {exception.Message}";
            GlobalLogger.Error(error, exception);
            return Result<T, string>.Err(error);
        }
    }

    /// <summary>以 UTF-8 保存 JSON；先写同目录临时文件，再替换目标，避免写入中断破坏上次配置。</summary>
    /// <typeparam name="T">需要序列化的配置类型。</typeparam>
    /// <param name="filePath">JSON 文件路径，父目录不存在时自动创建。</param>
    /// <param name="value">配置对象。</param>
    /// <returns>成功返回目标绝对路径，失败返回英文错误信息。</returns>
    public static Result<string, string> Write<T>(string filePath, T value) where T : notnull
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<string, string>.Err("JSON file path cannot be empty.");
        string? temporaryPath = null;
        try
        {
            // 序列化成功后才接触磁盘，临时文件与目标位于同一卷。
            var json = JsonSerializer.Serialize(value, Options);
            var fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
            return Result<string, string>.Ok(fullPath);
        }
        catch (Exception exception)
        {
            var error = $"Failed to write JSON file '{filePath}': {exception.Message}";
            GlobalLogger.Error(error, exception);
            return Result<string, string>.Err(error);
        }
        finally
        {
            // 清理失败不覆盖原始 Result，也不使正常调用重新抛出异常。
            try { if (temporaryPath is not null) File.Delete(temporaryPath); }
            catch (Exception exception) { GlobalLogger.Error("Failed to remove temporary JSON file.", exception); }
        }
    }
}
