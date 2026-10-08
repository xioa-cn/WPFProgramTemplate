using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace CsxPad.Wpf.Services;

public static class CSharpCodeFormattingService
{
    public static async Task<CSharpFormattingResult> FormatAsync(
        string code,
        int caretOffset,
        int selectionStart,
        int selectionLength,
        CancellationToken cancellationToken = default)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "CsxPad.Formatting",
            "CsxPad.Formatting",
            LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script)));
        var document = workspace.AddDocument(
            project.Id,
            "script.csx",
            SourceText.From(code));
        var formattedDocument = await Formatter.FormatAsync(
            document,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var changes = (await formattedDocument.GetTextChangesAsync(document, cancellationToken).ConfigureAwait(false))
            .OrderBy(change => change.Span.Start)
            .ToArray();
        var formattedText = (await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        var selectionEnd = selectionStart + selectionLength;

        return new CSharpFormattingResult(
            formattedText,
            MapOffset(caretOffset, changes),
            MapOffset(selectionStart, changes),
            Math.Max(0, MapOffset(selectionEnd, changes) - MapOffset(selectionStart, changes)));
    }

    private static int MapOffset(int offset, IReadOnlyList<TextChange> changes)
    {
        var delta = 0;
        foreach (var change in changes)
        {
            if (offset < change.Span.Start)
            {
                break;
            }

            var replacementLength = change.NewText?.Length ?? 0;
            if (offset <= change.Span.End)
            {
                if (change.Span.Length == 0)
                {
                    return change.Span.Start + delta + replacementLength;
                }

                var relativeOffset = offset - change.Span.Start;
                return change.Span.Start + delta + Math.Min(relativeOffset, replacementLength);
            }

            delta += replacementLength - change.Span.Length;
        }

        return offset + delta;
    }
}

public sealed record CSharpFormattingResult(
    string Text,
    int CaretOffset,
    int SelectionStart,
    int SelectionLength);
