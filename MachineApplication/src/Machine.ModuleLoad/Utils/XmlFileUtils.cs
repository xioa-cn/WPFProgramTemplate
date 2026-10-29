using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Machine.ModuleLoad.Logger;
using RustSharp;

namespace Machine.ModuleLoad.Utils;

public static class XmlFileUtils {
    public static Result<T, string> Read<T>(string filePath, Func<T>? whenMissing = null,
        XmlReaderSettings? readerSettings = null) where T : notnull {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<T, string>.Err("XML file path cannot be empty.");
        try {
            FileStream stream;
            try {
                stream = File.OpenRead(filePath);
            }
            catch (Exception exception) when (whenMissing is not null &&
                                              (exception is FileNotFoundException or DirectoryNotFoundException)) {
                var defaultValue = whenMissing();
                return Result<T, string>.Ok(defaultValue);
            }

            readerSettings ??= new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (stream)
            using (var reader = XmlReader.Create(stream, readerSettings)) {
                return new XmlSerializer(typeof(T)).Deserialize(reader) is T value
                    ? Result<T, string>.Ok(value)
                    : Result<T, string>.Err($"XML file '{filePath}' contains null or an unexpected value.");
            }
        }
        catch (Exception exception) {
            var error = $"Failed to read XML file '{filePath}': {exception.GetBaseException().Message}";
            GlobalLogger.Error(error, exception);
            return Result<T, string>.Err(error);
        }
    }

    public static Result<string, string> Write<T>(string filePath, T? value, XmlWriterSettings? settings = null)
        where T : notnull {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<string, string>.Err("XML file path cannot be empty.");
        if (value is null)
            return Result<string, string>.Err("XML value cannot be null.");
        string? temporaryPath = null;
        try {
            var serializer = new XmlSerializer(typeof(T));
            var fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
            settings ??= new XmlWriterSettings {
                Encoding = new UTF8Encoding(true, true),
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = false,
                CloseOutput = false
            };
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                using (var writer = XmlWriter.Create(stream, settings)) {
                    serializer.Serialize(writer, value);
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
            return Result<string, string>.Ok(fullPath);
        }
        catch (Exception exception) {
            var error = $"Failed to write XML file '{filePath}': {exception.GetBaseException().Message}";
            GlobalLogger.Error(error, exception);
            return Result<string, string>.Err(error);
        }
        finally {
            try {
                if (temporaryPath is not null)
                    File.Delete(temporaryPath);
            }
            catch (Exception exception) {
                GlobalLogger.Error("Failed to remove temporary XML file.", exception);
            }
        }
    }
}