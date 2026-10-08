using System.Windows;
using System.Windows.Input;
using CsxPad.Wpf.Services;
using ICSharpCode.AvalonEdit;

namespace CsxPad.Wpf.Helpers;

public static partial class CSharpEditorAssist
{
    private static readonly TimeSpan CommandChordTimeout = TimeSpan.FromSeconds(2);

    private static readonly DependencyProperty CommandChordDeadlineProperty = DependencyProperty.RegisterAttached(
        "CommandChordDeadline",
        typeof(DateTime),
        typeof(CSharpEditorAssist),
        new PropertyMetadata(DateTime.MinValue));

    private static void OnEditorPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            editor.SetValue(CommandChordDeadlineProperty, DateTime.UtcNow + CommandChordTimeout);
            CloseCompletion(editor);
            CloseQuickFixMenu(editor);
            CloseQuickInfo(editor);
            args.Handled = true;
            return;
        }

        var chordDeadline = (DateTime)editor.GetValue(CommandChordDeadlineProperty);
        if (key == Key.D &&
            Keyboard.Modifiers is ModifierKeys.Control or ModifierKeys.None &&
            chordDeadline >= DateTime.UtcNow)
        {
            editor.ClearValue(CommandChordDeadlineProperty);
            args.Handled = true;
            _ = FormatDocumentAsync(editor);
            return;
        }

        if (key == Key.C &&
            Keyboard.Modifiers is ModifierKeys.Control or ModifierKeys.None &&
            chordDeadline >= DateTime.UtcNow)
        {
            editor.ClearValue(CommandChordDeadlineProperty);
            CommentSelectedLines(editor);
            args.Handled = true;
            return;
        }

        if (key == Key.U &&
            Keyboard.Modifiers is ModifierKeys.Control or ModifierKeys.None &&
            chordDeadline >= DateTime.UtcNow)
        {
            editor.ClearValue(CommandChordDeadlineProperty);
            UncommentSelectedLines(editor);
            args.Handled = true;
            return;
        }

        if (key is not Key.LeftCtrl and not Key.RightCtrl)
        {
            editor.ClearValue(CommandChordDeadlineProperty);
        }
    }

    private static void CommentSelectedLines(TextEditor editor)
    {
        var document = editor.Document;
        if (document is null || document.LineCount == 0)
        {
            return;
        }

        var hasSelection = editor.SelectionLength > 0;
        var startOffset = hasSelection ? editor.SelectionStart : editor.TextArea.Caret.Offset;
        var endOffset = hasSelection
            ? editor.SelectionStart + editor.SelectionLength - 1
            : startOffset;
        var firstLineNumber = document.GetLineByOffset(Math.Clamp(startOffset, 0, document.TextLength)).LineNumber;
        var lastLineNumber = document.GetLineByOffset(Math.Clamp(endOffset, 0, document.TextLength)).LineNumber;
        var lines = document.Lines
            .Skip(firstLineNumber - 1)
            .Take(lastLineNumber - firstLineNumber + 1)
            .Select(line => new
            {
                line.Offset,
                IndentationLength = document.GetText(line).TakeWhile(char.IsWhiteSpace).Count()
            })
            .ToArray();

        document.BeginUpdate();
        try
        {
            foreach (var line in lines.Reverse())
            {
                document.Insert(line.Offset + line.IndentationLength, "//");
            }
        }
        finally
        {
            document.EndUpdate();
        }

        if (hasSelection)
        {
            var firstLine = document.GetLineByNumber(firstLineNumber);
            var lastLine = document.GetLineByNumber(lastLineNumber);
            editor.Select(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
        }
        else
        {
            var line = lines[0];
            var insertionOffset = line.Offset + line.IndentationLength;
            var caretOffset = startOffset >= insertionOffset ? startOffset + 2 : startOffset;
            editor.TextArea.Caret.Offset = Math.Min(caretOffset, document.TextLength);
        }

        editor.Focus();
    }

    private static void UncommentSelectedLines(TextEditor editor)
    {
        var document = editor.Document;
        if (document is null || document.LineCount == 0)
        {
            return;
        }

        var hasSelection = editor.SelectionLength > 0;
        var startOffset = hasSelection ? editor.SelectionStart : editor.TextArea.Caret.Offset;
        var endOffset = hasSelection
            ? editor.SelectionStart + editor.SelectionLength - 1
            : startOffset;
        var firstLineNumber = document.GetLineByOffset(Math.Clamp(startOffset, 0, document.TextLength)).LineNumber;
        var lastLineNumber = document.GetLineByOffset(Math.Clamp(endOffset, 0, document.TextLength)).LineNumber;
        var commentOffsets = document.Lines
            .Skip(firstLineNumber - 1)
            .Take(lastLineNumber - firstLineNumber + 1)
            .Select(line =>
            {
                var text = document.GetText(line);
                var indentationLength = text.TakeWhile(char.IsWhiteSpace).Count();
                return text.AsSpan(indentationLength).StartsWith("//", StringComparison.Ordinal)
                    ? line.Offset + indentationLength
                    : -1;
            })
            .Where(offset => offset >= 0)
            .ToArray();

        if (commentOffsets.Length == 0)
        {
            editor.Focus();
            return;
        }

        document.BeginUpdate();
        try
        {
            foreach (var offset in commentOffsets.Reverse())
            {
                document.Remove(offset, 2);
            }
        }
        finally
        {
            document.EndUpdate();
        }

        if (hasSelection)
        {
            var firstLine = document.GetLineByNumber(firstLineNumber);
            var lastLine = document.GetLineByNumber(lastLineNumber);
            editor.Select(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
        }
        else
        {
            var commentOffset = commentOffsets[0];
            var removedBeforeCaret = Math.Clamp(startOffset - commentOffset, 0, 2);
            editor.TextArea.Caret.Offset = Math.Clamp(
                startOffset - removedBeforeCaret,
                0,
                document.TextLength);
        }

        editor.Focus();
    }

    private static async Task FormatDocumentAsync(TextEditor editor)
    {
        var originalText = editor.Text;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        var caretOffset = editor.TextArea.Caret.Offset;
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        CSharpFormattingResult result;
        try
        {
            result = await Task.Run(() => CSharpCodeFormattingService.FormatAsync(
                originalText,
                caretOffset,
                selectionStart,
                selectionLength));
        }
        catch (Exception)
        {
            return;
        }

        if (!string.Equals(editor.Text, originalText, StringComparison.Ordinal) ||
            string.Equals(result.Text, originalText, StringComparison.Ordinal))
        {
            return;
        }

        editor.Document.Replace(0, editor.Document.TextLength, result.Text);
        if (result.SelectionLength > 0)
        {
            editor.Select(
                Math.Clamp(result.SelectionStart, 0, editor.Document.TextLength),
                Math.Clamp(
                    result.SelectionLength,
                    0,
                    editor.Document.TextLength - Math.Clamp(result.SelectionStart, 0, editor.Document.TextLength)));
        }
        else
        {
            editor.TextArea.Caret.Offset = Math.Clamp(result.CaretOffset, 0, editor.Document.TextLength);
        }

        editor.Focus();
    }
}
