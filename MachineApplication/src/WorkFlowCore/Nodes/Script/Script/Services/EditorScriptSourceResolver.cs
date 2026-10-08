using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CsxPad.Wpf.Services;

internal sealed class EditorScriptSourceResolver(SourceFileResolver fileResolver)
    : SourceReferenceResolver
{
    private readonly SourceFileResolver _fileResolver = fileResolver;

    public override string? NormalizePath(string path, string? baseFilePath) =>
        _fileResolver.NormalizePath(path, baseFilePath);

    public override string? ResolveReference(string path, string? baseFilePath) =>
        _fileResolver.ResolveReference(path, baseFilePath);

    public override Stream OpenRead(string resolvedPath)
    {
        using var stream = _fileResolver.OpenRead(resolvedPath);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: false);
        var source = reader.ReadToEnd();
        var preparedSource = CSharpScriptDirectiveService.PrepareForEditorAnalysis(source);
        return new MemoryStream(Encoding.UTF8.GetBytes(preparedSource), writable: false);
    }

    public override bool Equals(object? other) =>
        other is EditorScriptSourceResolver resolver && _fileResolver.Equals(resolver._fileResolver);

    public override int GetHashCode() => _fileResolver.GetHashCode();
}
