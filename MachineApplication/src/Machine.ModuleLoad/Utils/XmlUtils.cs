using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using RustSharp;

namespace Machine.ModuleLoad.Utils;

public static class XmlUtils {
    public static Result<string, Exception> ToXml<T>(this T obj, XmlWriterSettings? writerSettings = null) {
        try {
            ArgumentNullException.ThrowIfNull(obj);
            writerSettings ??= new XmlWriterSettings {
                Encoding = new UTF8Encoding(false, true),
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize
            };
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, writerSettings)) {
                new XmlSerializer(typeof(T)).Serialize(writer, obj);
            }

            return Result<string, Exception>.Ok(Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception exception) {
            return Result<string, Exception>.Err(exception);
        }
    }

    public static Result<T, Exception> XmlToModel<T>(this string xmlStr, XmlReaderSettings? readerSettings = null)
        where T : notnull {
        try {
            ArgumentException.ThrowIfNullOrWhiteSpace(xmlStr);
            readerSettings ??= new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var textReader = new StringReader(xmlStr.TrimStart('\uFEFF'));
            using var reader = XmlReader.Create(textReader, readerSettings);
            return new XmlSerializer(typeof(T)).Deserialize(reader) is T value
                ? Result<T, Exception>.Ok(value)
                : Result<T, Exception>.Err(new InvalidOperationException("XML contains null or an unexpected value."));
        }
        catch (Exception exception) {
            return Result<T, Exception>.Err(exception);
        }
    }
}