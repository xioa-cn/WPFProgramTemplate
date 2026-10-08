using System.Text.RegularExpressions;

namespace CsxPad.Wpf.Helpers;

public static class CSharpUsingDirectiveService
{
    private static readonly Regex UsingDirectiveRegex = new(
        @"^[ \t]*using[ \t]+[A-Za-z_][A-Za-z0-9_.]*[ \t]*;[ \t]*(?:\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex LeadingDirectiveRegex = new(
        @"\A(?:[ \t]*#(?:r|load)\b[^\r\n]*(?:\r?\n|$))*",
        RegexOptions.Compiled);

    public static ScriptTextInsertion? CreateInsertion(string script, string typeNamespace)
    {
        if (string.IsNullOrWhiteSpace(typeNamespace) || Regex.IsMatch(
                script,
                $@"^[ \t]*using[ \t]+{Regex.Escape(typeNamespace)}[ \t]*;",
                RegexOptions.Multiline))
        {
            return null;
        }

        var usingMatches = UsingDirectiveRegex.Matches(script);
        var insertionOffset = usingMatches.Count > 0
            ? usingMatches[usingMatches.Count - 1].Index + usingMatches[usingMatches.Count - 1].Length
            : LeadingDirectiveRegex.Match(script).Length;
        var newLine = script.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : Environment.NewLine;
        var needsLeadingNewLine = insertionOffset > 0 && script[insertionOffset - 1] is not '\r' and not '\n';
        var insertionText = $"{(needsLeadingNewLine ? newLine : string.Empty)}using {typeNamespace};{newLine}";

        return new ScriptTextInsertion(insertionOffset, insertionText);
    }
}

public sealed record ScriptTextInsertion(int Offset, string Text);
