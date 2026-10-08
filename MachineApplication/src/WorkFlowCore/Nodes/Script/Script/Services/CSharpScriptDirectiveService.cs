using System.Text.RegularExpressions;

namespace CsxPad.Wpf.Services;

internal static partial class CSharpScriptDirectiveService
{
    public static string PrepareForEditorAnalysis(string script) =>
        NormalizeLoadDirectives(SanitizeHostReferenceDirectives(script));

    public static string NormalizeLoadDirectives(string script) => LoadDirectiveWithSemicolonRegex().Replace(
        script,
        match => match.Value.Remove(match.Groups["semicolon"].Index - match.Index, 1).Insert(
            match.Groups["semicolon"].Index - match.Index,
            " "));

    private static string SanitizeHostReferenceDirectives(string script) =>
        HostReferenceDirectiveRegex().Replace(
            script,
            match => new string(' ', match.Length));

    [GeneratedRegex(
        "^[ \\t]*#load[ \\t]+\"[^\"\\r\\n]+\"[ \\t]*(?<semicolon>;)[ \\t]*(?=\\r?$)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LoadDirectiveWithSemicolonRegex();

    [GeneratedRegex(
        "^[ \\t]*#r[ \\t]+\"(?:nuget|framework):[^\"\\r\\n]*\"[^\\r\\n]*",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HostReferenceDirectiveRegex();
}
