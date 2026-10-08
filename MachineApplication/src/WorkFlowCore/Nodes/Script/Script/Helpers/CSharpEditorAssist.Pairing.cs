using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace CsxPad.Wpf.Helpers;

public static partial class CSharpEditorAssist
{
    private static bool TryHandleAutomaticPairing(
        TextEditor editor,
        TextArea textArea,
        TextCompositionEventArgs args)
    {
        if (textArea.Document is not { } document || args.Text.Length != 1)
        {
            return false;
        }

        var character = args.Text[0];
        var caretOffset = textArea.Caret.Offset;
        var isCodePosition = CSharpScriptSymbolCatalog.IsCompletionContext(editor.Text, caretOffset);

        if (character is ')' or '}' &&
            isCodePosition &&
            editor.SelectionLength == 0 &&
            IsCharacterAt(document, caretOffset, character))
        {
            textArea.Caret.Offset = caretOffset + 1;
            args.Handled = true;
            return true;
        }

        if (character == '"' &&
            editor.SelectionLength == 0 &&
            IsCharacterAt(document, caretOffset, '"') &&
            !IsEscapedAt(document, caretOffset))
        {
            textArea.Caret.Offset = caretOffset + 1;
            args.Handled = true;
            return true;
        }

        if (character == '(' && isCodePosition)
        {
            InsertSimplePair(editor, "(", ")");
            args.Handled = true;
            return true;
        }

        if (character == '"' && (isCodePosition || IsDirectiveValuePosition(document, caretOffset)))
        {
            InsertSimplePair(editor, "\"", "\"");
            args.Handled = true;
            return true;
        }

        if (character == '{' && isCodePosition)
        {
            InsertBraceBlock(editor);
            args.Handled = true;
            return true;
        }

        return false;
    }

    private static void InsertSimplePair(TextEditor editor, string opening, string closing)
    {
        var document = editor.Document;
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        var selectedText = selectionLength > 0
            ? document.GetText(selectionStart, selectionLength)
            : string.Empty;
        var insertion = opening + selectedText + closing;

        document.Replace(selectionStart, selectionLength, insertion);
        if (selectionLength > 0)
        {
            editor.Select(selectionStart + opening.Length, selectedText.Length);
        }
        else
        {
            editor.TextArea.Caret.Offset = selectionStart + opening.Length;
        }
    }

    private static void InsertBraceBlock(TextEditor editor)
    {
        var document = editor.Document;
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        var line = document.GetLineByOffset(selectionStart);
        var linePrefix = document.GetText(line.Offset, selectionStart - line.Offset);
        var indentation = new string(linePrefix.TakeWhile(char.IsWhiteSpace).ToArray());
        var indentationUnit = editor.Options.ConvertTabsToSpaces
            ? new string(' ', Math.Max(1, editor.Options.IndentationSize))
            : "\t";
        var bodyIndentation = indentation + indentationUnit;
        var newLine = GetDocumentNewLine(document);

        if (selectionLength > 0)
        {
            var selectedText = document.GetText(selectionStart, selectionLength);
            var indentedSelection = selectedText.Replace(
                newLine,
                newLine + bodyIndentation,
                StringComparison.Ordinal);
            var insertion = "{" + newLine + bodyIndentation + indentedSelection +
                            newLine + indentation + "}";
            document.Replace(selectionStart, selectionLength, insertion);
            editor.Select(
                selectionStart + 1 + newLine.Length + bodyIndentation.Length,
                indentedSelection.Length);
            return;
        }

        var trailingLength = line.EndOffset - selectionStart;
        var trailingText = trailingLength > 0
            ? document.GetText(selectionStart, trailingLength)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(trailingText))
        {
            document.Insert(selectionStart, "{}");
            editor.TextArea.Caret.Offset = selectionStart + 1;
            return;
        }

        var block = "{" + newLine + bodyIndentation + newLine + indentation + "}";
        document.Replace(selectionStart, trailingLength, block);
        editor.TextArea.Caret.Offset = selectionStart + 1 + newLine.Length + bodyIndentation.Length;
    }

    private static string GetDocumentNewLine(TextDocument document)
    {
        var lineWithDelimiter = document.Lines.FirstOrDefault(line => line.DelimiterLength > 0);
        return lineWithDelimiter is null
            ? Environment.NewLine
            : document.GetText(lineWithDelimiter.EndOffset, lineWithDelimiter.DelimiterLength);
    }

    private static bool IsDirectiveValuePosition(TextDocument document, int caretOffset)
    {
        var line = document.GetLineByOffset(caretOffset);
        var prefix = document.GetText(line.Offset, caretOffset - line.Offset).TrimStart();
        return (prefix.StartsWith("#load", StringComparison.Ordinal) ||
                prefix.StartsWith("#r", StringComparison.Ordinal)) &&
               prefix.Count(character => character == '"') % 2 == 0;
    }

    private static bool IsCharacterAt(TextDocument document, int offset, char character) =>
        offset >= 0 && offset < document.TextLength && document.GetCharAt(offset) == character;

    private static bool IsEscapedAt(TextDocument document, int offset)
    {
        var backslashCount = 0;
        for (var index = offset - 1; index >= 0 && document.GetCharAt(index) == '\\'; index--)
        {
            backslashCount++;
        }

        return backslashCount % 2 != 0;
    }
}
