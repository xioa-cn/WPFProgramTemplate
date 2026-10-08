using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using CsxPad.Wpf.Services;
using CsxPad.Wpf.Scripting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;

namespace CsxPad.Wpf.Helpers;

public static partial class CSharpEditorAssist
{
    private const double MinimumEditorFontSize = 9;
    private const double MaximumEditorFontSize = 32;
    private const double EditorZoomStep = 1;

    private static readonly Brush PopupBackground = FrozenBrush("#20242A");
    private static readonly Brush PopupForeground = FrozenBrush("#E8EAED");
    private static readonly Brush PopupBorder = FrozenBrush("#454B55");
    private static readonly Brush PopupSelection = FrozenBrush("#24485A");
    private static readonly Brush MutedForeground = FrozenBrush("#9AA4B2");
    private static readonly Brush KeywordBrush = FrozenBrush("#569CD6");
    private static readonly Brush TypeBrush = FrozenBrush("#4EC9B0");
    private static readonly Brush InterfaceBrush = FrozenBrush("#B8D7A3");
    private static readonly Brush EnumBrush = FrozenBrush("#C586C0");
    private static readonly Brush VariableBrush = FrozenBrush("#4FC1FF");
    private static readonly Brush MemberBrush = FrozenBrush("#DCDCAA");
    private static readonly Brush QuickFixBrush = FrozenBrush("#FFCC66");
    private static readonly Brush BadgeForeground = FrozenBrush("#111418");
    private static readonly Brush ErrorBrush = FrozenBrush("#F14C4C");
    private static readonly Brush WarningBrush = FrozenBrush("#CCA700");
    private static readonly Pen ColumnRulerPen = FrozenPen("#30353D", 1);
    private static readonly ControlTemplate QuickInfoToolTipTemplate = CreateQuickInfoToolTipTemplate();

    private static readonly CompletionEntry[] Keywords =
    [
        Entry("async", "keyword", "Marks a method or expression as asynchronous."),
        Entry("await", "keyword", "Asynchronously waits for a task to complete."),
        Entry("bool", "keyword", "Boolean value type."),
        Entry("break", "keyword", "Terminates the closest loop or switch."),
        Entry("case", "keyword", "Defines a switch section."),
        Entry("catch", "keyword", "Handles an exception."),
        Entry("class", "keyword", "Declares a reference type."),
        Entry("const", "keyword", "Declares a compile-time constant."),
        Entry("continue", "keyword", "Starts the next loop iteration."),
        Entry("decimal", "keyword", "High precision decimal value type."),
        Entry("else", "keyword", "Provides the alternative conditional branch."),
        Entry("enum", "keyword", "Declares an enumeration."),
        Entry("false", "keyword", "Boolean false literal."),
        Entry("for", "keyword", "Creates a counted loop."),
        Entry("foreach", "keyword", "Iterates over a sequence."),
        Entry("if", "keyword", "Runs code conditionally."),
        Entry("int", "keyword", "32-bit signed integer type."),
        Entry("new", "keyword", "Creates a value or object."),
        Entry("null", "keyword", "Null reference literal."),
        Entry("record", "keyword", "Declares a data-focused reference type."),
        Entry("return", "keyword", "Returns control and an optional value."),
        Entry("string", "keyword", "UTF-16 text type."),
        Entry("switch", "keyword", "Selects a branch from patterns."),
        Entry("throw", "keyword", "Raises an exception."),
        Entry("true", "keyword", "Boolean true literal."),
        Entry("try", "keyword", "Starts an exception handling block."),
        Entry("using", "keyword", "Imports a namespace or disposes a resource."),
        Entry("var", "keyword", "Infers the local variable type."),
        Entry("while", "keyword", "Repeats while a condition is true."),
        Entry("DateTime", "type", "Represents a date and time."),
        Entry("Guid", "type", "Represents a globally unique identifier."),
        Entry("List", "type", "A strongly typed mutable list."),
        Entry("Dictionary", "type", "A key/value collection."),
        Entry("IEnumerable", "interface", "A sequence that can be enumerated."),
        Entry("IQueryable", "interface", "A query with an expression tree."),
        Entry("Task", "type", "Represents asynchronous work."),
        Entry("Console", "type", "Provides standard input and output streams."),
        Entry("HttpClient", "type", "Sends HTTP requests and receives responses."),
        Entry("JsonSerializer", "type", "Serializes values to and from JSON.")
    ];

    private static readonly CompletionEntry[] TypeMemberModifiers =
    [
        Entry("public", "modifier", "Declares unrestricted accessibility."),
        Entry("private", "modifier", "Restricts accessibility to the containing type."),
        Entry("protected", "modifier", "Allows access from the containing type and derived types."),
        Entry("internal", "modifier", "Restricts accessibility to the current assembly."),
        Entry("static", "modifier", "Declares a member that belongs to its type."),
        Entry("abstract", "modifier", "Declares an incomplete type or member that must be implemented."),
        Entry("sealed", "modifier", "Prevents inheritance or further overriding."),
        Entry("partial", "modifier", "Splits a declaration across multiple parts."),
        Entry("readonly", "modifier", "Prevents reassignment outside the allowed initialization context."),
        Entry("const", "modifier", "Declares a compile-time constant."),
        Entry("volatile", "modifier", "Marks a field for special multi-threaded access handling."),
        Entry("virtual", "modifier", "Allows a member to be overridden."),
        Entry("override", "modifier", "Overrides an inherited virtual or abstract member."),
        Entry("new", "modifier", "Explicitly hides an inherited member."),
        Entry("extern", "modifier", "Declares a member implemented externally."),
        Entry("unsafe", "modifier", "Allows unsafe code in the declaration."),
        Entry("async", "modifier", "Declares an asynchronous method or function."),
        Entry("required", "modifier", "Requires callers to initialize the member."),
        Entry("ref", "modifier", "Declares ref-oriented storage or return semantics."),
        Entry("fixed", "modifier", "Declares a fixed-size buffer in an unsafe context.")
    ];

    private static readonly CompletionEntry[] TopLevelModifiers =
    [
        Entry("public", "modifier", "Declares unrestricted accessibility."),
        Entry("internal", "modifier", "Restricts accessibility to the current assembly."),
        Entry("file", "modifier", "Restricts a top-level type to the current source file."),
        Entry("static", "modifier", "Declares a static type or local function."),
        Entry("abstract", "modifier", "Declares an incomplete type that must be inherited."),
        Entry("sealed", "modifier", "Prevents inheritance."),
        Entry("partial", "modifier", "Splits a declaration across multiple parts."),
        Entry("readonly", "modifier", "Declares an immutable struct."),
        Entry("ref", "modifier", "Declares a ref struct or ref-oriented local."),
        Entry("unsafe", "modifier", "Allows unsafe code in the declaration."),
        Entry("async", "modifier", "Declares an asynchronous local function.")
    ];

    private static readonly CompletionEntry[] ParameterModifiers =
    [
        Entry("ref", "modifier", "Passes an argument by reference."),
        Entry("out", "modifier", "Passes an output argument by reference."),
        Entry("in", "modifier", "Passes a readonly argument by reference."),
        Entry("params", "modifier", "Accepts a variable number of arguments."),
        Entry("this", "modifier", "Marks the receiver parameter of an extension method."),
        Entry("scoped", "modifier", "Restricts the lifetime of a ref-like value."),
        Entry("readonly", "modifier", "Adds readonly semantics to a ref parameter.")
    ];

    private static readonly CompletionEntry[] ExecutableModifiers =
    [
        Entry("const", "modifier", "Declares a local compile-time constant."),
        Entry("ref", "modifier", "Declares ref-oriented local storage."),
        Entry("scoped", "modifier", "Restricts the lifetime of a ref-like local."),
        Entry("static", "modifier", "Prevents a local function from capturing state."),
        Entry("async", "modifier", "Declares an asynchronous local function."),
        Entry("unsafe", "modifier", "Allows unsafe code in the local declaration.")
    ];

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(CSharpEditorAssist),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CompletionCatalogProperty = DependencyProperty.RegisterAttached(
        "CompletionCatalog",
        typeof(PackageCompletionCatalog),
        typeof(CSharpEditorAssist),
        new PropertyMetadata(PackageCompletionCatalog.Empty, OnCompletionCatalogChanged));

    public static readonly DependencyProperty DebugPauseProperty = DependencyProperty.RegisterAttached(
        "DebugPause",
        typeof(ScriptDebugPause),
        typeof(CSharpEditorAssist),
        new PropertyMetadata(null, OnDebugPauseChanged));

    public static readonly DependencyProperty ScriptPathProperty = DependencyProperty.RegisterAttached(
        "ScriptPath",
        typeof(string),
        typeof(CSharpEditorAssist),
        new PropertyMetadata(null, OnScriptPathChanged));

    private static readonly DependencyProperty CompletionWindowProperty = DependencyProperty.RegisterAttached(
        "CompletionWindow",
        typeof(CompletionWindow),
        typeof(CSharpEditorAssist));

    private static readonly DependencyProperty QuickFixMenuProperty = DependencyProperty.RegisterAttached(
        "QuickFixMenu",
        typeof(ContextMenu),
        typeof(CSharpEditorAssist));

    private static readonly DependencyProperty OwnerEditorProperty = DependencyProperty.RegisterAttached(
        "OwnerEditor",
        typeof(TextEditor),
        typeof(CSharpEditorAssist));

    private static readonly DependencyProperty AnalysisStateProperty = DependencyProperty.RegisterAttached(
        "AnalysisState",
        typeof(EditorAnalysisState),
        typeof(CSharpEditorAssist));

    private static readonly DependencyProperty QuickInfoToolTipProperty = DependencyProperty.RegisterAttached(
        "QuickInfoToolTip",
        typeof(ToolTip),
        typeof(CSharpEditorAssist));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static PackageCompletionCatalog GetCompletionCatalog(DependencyObject element) =>
        (PackageCompletionCatalog)element.GetValue(CompletionCatalogProperty);

    public static void SetCompletionCatalog(DependencyObject element, PackageCompletionCatalog value) =>
        element.SetValue(CompletionCatalogProperty, value);

    public static ScriptDebugPause? GetDebugPause(DependencyObject element) =>
        element.GetValue(DebugPauseProperty) as ScriptDebugPause;

    public static void SetDebugPause(DependencyObject element, ScriptDebugPause? value) =>
        element.SetValue(DebugPauseProperty, value);

    public static string? GetScriptPath(DependencyObject element) =>
        element.GetValue(ScriptPathProperty) as string;

    public static void SetScriptPath(DependencyObject element, string? value) =>
        element.SetValue(ScriptPathProperty, value);

    private static void OnDebugPauseChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        CloseQuickInfo(dependencyObject);
    }

    private static void OnScriptPathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TextEditor editor)
        {
            CloseCompletion(editor);
            CloseQuickInfo(editor);
            (editor.GetValue(AnalysisStateProperty) as EditorAnalysisState)?.Schedule();
        }
    }

    private static void OnCompletionCatalogChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TextEditor editor)
        {
            CloseCompletion(editor);
            CloseQuickFixMenu(editor);
            CloseQuickInfo(editor);
            (editor.GetValue(AnalysisStateProperty) as EditorAnalysisState)?.Schedule();
        }
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextEditor editor)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            ApplyHighlighting(editor);
            ConfigureColumnRuler(editor);
            editor.TextArea.SetValue(OwnerEditorProperty, editor);
            editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(editor.Options);
            editor.TextArea.TextEntered += OnTextEntered;
            editor.TextArea.TextEntering += OnTextEntering;
            editor.TextArea.KeyDown += OnKeyDown;
            editor.PreviewKeyDown += OnEditorPreviewKeyDown;
            editor.PreviewMouseWheel += OnPreviewMouseWheel;
            editor.MouseHover += OnMouseHover;
            editor.MouseHoverStopped += OnMouseHoverStopped;
            editor.Loaded += OnEditorLoaded;
            var analysisState = new EditorAnalysisState(editor);
            editor.SetValue(AnalysisStateProperty, analysisState);
            analysisState.Schedule();
        }
        else
        {
            editor.TextArea.TextEntered -= OnTextEntered;
            editor.TextArea.TextEntering -= OnTextEntering;
            editor.TextArea.KeyDown -= OnKeyDown;
            editor.PreviewKeyDown -= OnEditorPreviewKeyDown;
            editor.PreviewMouseWheel -= OnPreviewMouseWheel;
            editor.MouseHover -= OnMouseHover;
            editor.MouseHoverStopped -= OnMouseHoverStopped;
            editor.Loaded -= OnEditorLoaded;
            (editor.GetValue(AnalysisStateProperty) as EditorAnalysisState)?.Dispose();
            editor.ClearValue(AnalysisStateProperty);
            editor.TextArea.ClearValue(OwnerEditorProperty);
            editor.TextArea.IndentationStrategy = null;
            CloseCompletion(editor);
            CloseQuickFixMenu(editor);
            CloseQuickInfo(editor);
        }
    }

    private static void OnEditorLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextEditor editor)
        {
            ConfigureColumnRuler(editor);
        }
    }

    private static void ConfigureColumnRuler(TextEditor editor)
    {
        editor.Options.ShowColumnRuler = true;
        editor.Options.ColumnRulerPosition = 120;
        editor.TextArea.TextView.ColumnRulerPen = ColumnRulerPen;
        editor.TextArea.TextView.Redraw();
    }

    private static void OnMouseHover(object sender, MouseEventArgs args)
    {
        if (sender is not TextEditor editor || editor.Document is null)
        {
            return;
        }

        var position = editor.GetPositionFromPoint(args.GetPosition(editor));
        if (position is null)
        {
            CloseQuickInfo(editor);
            return;
        }

        var offset = editor.Document.GetOffset(position.Value.Line, position.Value.Column);
        var debugPause = GetDebugPause(editor);
        if (debugPause is not null)
        {
            var expression = ReadQualifiedIdentifierAt(editor.Document, offset);
            if (TryResolveDebugValue(debugPause, expression, out var debugValue))
            {
                ShowQuickInfo(editor, CreateDebugValueContent(expression, debugValue));
                return;
            }
        }

        var state = editor.GetValue(AnalysisStateProperty) as EditorAnalysisState;
        var diagnostic = state?.Diagnostics.FirstOrDefault(item =>
            offset >= item.Offset && offset <= item.Offset + item.Length);
        if (diagnostic is not null)
        {
            ShowQuickInfo(editor, CreateDiagnosticContent(diagnostic));
            return;
        }

        var quickInfo = CSharpSemanticCompletionService.GetQuickInfo(
            editor.Text,
            offset,
            GetCompletionCatalog(editor).ReferencePaths,
            GetScriptPath(editor));
        if (quickInfo is null)
        {
            CloseQuickInfo(editor);
            return;
        }

        ShowQuickInfo(editor, CreateSymbolContent(quickInfo));
    }

    private static void OnMouseHoverStopped(object sender, MouseEventArgs args)
    {
        if (sender is TextEditor editor)
        {
            CloseQuickInfo(editor);
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (sender is not TextEditor editor || Keyboard.Modifiers != ModifierKeys.Control || args.Delta == 0)
        {
            return;
        }

        var direction = args.Delta > 0 ? EditorZoomStep : -EditorZoomStep;
        editor.FontSize = Math.Clamp(
            editor.FontSize + direction,
            MinimumEditorFontSize,
            MaximumEditorFontSize);
        args.Handled = true;
    }

    private static void ApplyHighlighting(TextEditor editor)
    {
        const string suffix = "Resources.CsxDark.xshd";
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
        if (resourceName is null)
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }

        try
        {
            using var reader = XmlReader.Create(stream);
            editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (HighlightingDefinitionInvalidException)
        {
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }
    }

    private static void OnTextEntered(object sender, TextCompositionEventArgs args)
    {
        if (sender is not TextArea textArea || textArea.Document is null || string.IsNullOrEmpty(args.Text))
        {
            return;
        }

        var editor = FindEditor(textArea);
        if (editor is null)
        {
            return;
        }

        if (!CSharpScriptSymbolCatalog.IsCompletionContext(editor.Text, textArea.Caret.Offset))
        {
            CloseCompletion(editor);
            return;
        }

        var character = args.Text[0];
        if (character == '.')
        {
            var dotOffset = textArea.Caret.Offset - 1;
            if (!CSharpScriptSymbolCatalog.IsMemberAccessDot(editor.Text, dotOffset))
            {
                CloseCompletion(editor);
                return;
            }

            ShowCompletion(
                editor,
                GetMemberEntries(editor, dotOffset),
                textArea.Caret.Offset,
                automatic: true);
            return;
        }

        if (char.IsLetter(character) || character == '_')
        {
            if (!CSharpScriptSymbolCatalog.IsAutomaticCompletionContext(editor.Text, textArea.Caret.Offset))
            {
                CloseCompletion(editor);
                return;
            }

            var startOffset = FindWordStart(textArea.Document, textArea.Caret.Offset);
            var queryLength = textArea.Caret.Offset - startOffset;
            if (queryLength >= 2 && GetCompletionWindow(editor) is null)
            {
                var isMemberAccess = startOffset > 0 && textArea.Document.GetCharAt(startOffset - 1) == '.';
                if (!isMemberAccess && CSharpScriptSymbolCatalog.IsDeclarationIdentifier(
                        editor.Text,
                        textArea.Caret.Offset))
                {
                    return;
                }

                var entries = isMemberAccess
                    ? GetMemberEntries(editor, startOffset - 1)
                    : GetRootEntries(editor, textArea.Document.GetText(startOffset, queryLength), automatic: true);
                ShowCompletion(editor, entries, startOffset, automatic: true);
            }
            else if (GetCompletionWindow(editor) is not null)
            {
                RefreshCompletionSelection(editor);
            }
        }
    }

    private static void OnTextEntering(object sender, TextCompositionEventArgs args)
    {
        if (sender is not TextArea textArea || string.IsNullOrEmpty(args.Text))
        {
            return;
        }

        var editor = FindEditor(textArea);
        if (editor is not null && TryHandleAutomaticPairing(editor, textArea, args))
        {
            CloseCompletion(editor);
            return;
        }

        var window = editor is null ? null : GetCompletionWindow(editor);
        if (window is null)
        {
            return;
        }

        var character = args.Text[0];
        if (!char.IsLetterOrDigit(character) && character != '_')
        {
            if (window.CompletionList.SelectedItem is CompletionEntry selected &&
                selected.ShouldCommit(character))
            {
                window.CompletionList.RequestInsertion(args);
            }
            else
            {
                window.Close();
            }
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is not TextArea textArea || textArea.Document is null)
        {
            return;
        }

        var editor = FindEditor(textArea);
        if (editor is null)
        {
            return;
        }

        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        var completionWindow = GetCompletionWindow(editor);
        if (completionWindow is not null && (key is Key.Enter or Key.Tab) &&
            Keyboard.Modifiers == ModifierKeys.None)
        {
            completionWindow.CompletionList.RequestInsertion(args);
            args.Handled = true;
            return;
        }

        if (key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            args.Handled = ShowUsingQuickFix(editor);
            return;
        }

        if (key != Key.Space || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (!CSharpScriptSymbolCatalog.IsCompletionContext(editor.Text, textArea.Caret.Offset))
        {
            CloseCompletion(editor);
            args.Handled = true;
            return;
        }

        var startOffset = FindWordStart(textArea.Document, textArea.Caret.Offset);
        var entries = startOffset > 0 && textArea.Document.GetCharAt(startOffset - 1) == '.'
            ? GetMemberEntries(editor, startOffset - 1)
            : GetRootEntries(editor, textArea.Document.GetText(
                startOffset,
                textArea.Caret.Offset - startOffset), automatic: false);
        ShowCompletion(editor, entries, startOffset, automatic: false);
        args.Handled = true;
    }

    private static bool ShowUsingQuickFix(TextEditor editor)
    {
        var identifier = ReadIdentifierAt(editor.Document, editor.TextArea.Caret.Offset);
        if (identifier.Length == 0)
        {
            return false;
        }

        var importedNamespaces = GetImportedNamespaces(editor.Text);
        var namespaces = GetCompletionCatalog(editor)
            .FindTypes(identifier)
            .Where(type => type.Namespace.Length > 0 && !importedNamespaces.Contains(type.Namespace))
            .Select(type => type.Namespace)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeNamespace => typeNamespace, StringComparer.Ordinal)
            .ToArray();
        if (namespaces.Length == 0)
        {
            return false;
        }

        CloseCompletion(editor);
        CloseQuickFixMenu(editor);
        var caretRectangle = editor.TextArea.Caret.CalculateCaretRectangle();
        var menu = new ContextMenu
        {
            PlacementTarget = editor.TextArea,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = caretRectangle.Left,
            VerticalOffset = caretRectangle.Bottom,
            Background = PopupBackground,
            Foreground = PopupForeground,
            BorderBrush = PopupBorder,
            BorderThickness = new Thickness(1),
            MinWidth = 360
        };
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 12, 7)));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, PopupForeground));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Triggers.Add(new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true,
            Setters = { new Setter(Control.BackgroundProperty, PopupSelection) }
        });
        menu.ItemContainerStyle = itemStyle;

        foreach (var typeNamespace in namespaces)
        {
            var item = new MenuItem
            {
                Header = $"using {typeNamespace};",
                ToolTip = $"Import namespace for {identifier}"
            };
            item.Click += (_, _) =>
            {
                AddUsingDirective(editor.TextArea, typeNamespace);
                editor.Focus();
            };
            menu.Items.Add(item);
        }

        menu.Opened += (_, _) => menu.Dispatcher.BeginInvoke(
            new Action(() => Keyboard.Focus((MenuItem)menu.Items[0])),
            DispatcherPriority.Input);
        menu.Closed += (_, _) => editor.ClearValue(QuickFixMenuProperty);
        editor.SetValue(QuickFixMenuProperty, menu);
        menu.IsOpen = true;
        return true;
    }

    private static void AddUsingDirective(TextArea textArea, string typeNamespace)
    {
        var insertion = CSharpUsingDirectiveService.CreateInsertion(textArea.Document.Text, typeNamespace);
        if (insertion is null)
        {
            return;
        }

        var caretAnchor = textArea.Document.CreateAnchor(textArea.Caret.Offset);
        caretAnchor.MovementType = AnchorMovementType.AfterInsertion;
        textArea.Document.Insert(insertion.Offset, insertion.Text);
        textArea.Caret.Offset = caretAnchor.Offset;
    }

    private static IEnumerable<CompletionEntry> GetRootEntries(
        TextEditor editor,
        string query,
        bool automatic)
    {
        var importedNamespaces = GetImportedNamespaces(editor.Text);
        var expectedTypeName = CSharpScriptSymbolCatalog.GetExpectedTypeName(
            editor.Text,
            editor.TextArea.Caret.Offset);
        var catalog = GetCompletionCatalog(editor);
        var visibleSymbols = CSharpSemanticCompletionService.GetVisibleSymbols(
                editor.Text,
                editor.TextArea.Caret.Offset,
                catalog.ReferencePaths,
                GetScriptPath(editor))
            .Select(symbol => new CompletionEntry(
                symbol.Name,
                symbol.Name,
                symbol.Kind,
                symbol.Description,
                FormatGenericName(symbol.Name, symbol.GenericArity)));
        var packageTypes = catalog.Types
            .Where(type => !automatic || type.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Select(type =>
        {
            var requiresUsing = type.Namespace.Length > 0 && !importedNamespaces.Contains(type.Namespace);
            var description = type.Namespace.Length == 0
                ? $"Public type from an installed package."
                : $"{type.FullName}\nPublic type from an installed package.";
            var priority = string.Equals(type.Name, expectedTypeName, StringComparison.Ordinal) ? 100 : (double?)null;
            return new CompletionEntry(
                type.Name,
                type.Name,
                type.Kind,
                description,
                sortPriority: priority,
                completion: requiresUsing
                    ? (textArea, segment) => CompleteTypeWithUsing(
                        textArea,
                        segment,
                        type.Name,
                        type.Namespace)
                    : null,
                identity: type.FullName);
        });

        return visibleSymbols
            .Concat(packageTypes)
            .Concat(GetModifierEntries(editor.Text, editor.TextArea.Caret.Offset))
            .Concat(Keywords)
            .Concat(GetSnippetEntries(editor));
    }

    private static IEnumerable<CompletionEntry> GetModifierEntries(string script, int caretOffset) =>
        CSharpScriptSymbolCatalog.GetDeclarationContext(script, caretOffset) switch
        {
            ScriptDeclarationContext.TopLevel => TopLevelModifiers,
            ScriptDeclarationContext.TypeMember => TypeMemberModifiers,
            ScriptDeclarationContext.Parameter => ParameterModifiers,
            ScriptDeclarationContext.Executable => ExecutableModifiers,
            _ => []
        };

    private static IEnumerable<CompletionEntry> GetMemberEntries(TextEditor editor, int dotOffset)
    {
        var expression = ReadQualifiedIdentifierBefore(editor.Document, dotOffset);
        var catalog = GetCompletionCatalog(editor);
        var semanticResult = CSharpSemanticCompletionService.GetMemberResult(
            editor.Text,
            dotOffset,
            catalog.ReferencePaths,
            GetScriptPath(editor));
        if (semanticResult.ReceiverResolved)
        {
            return semanticResult.Members.Select(member => new CompletionEntry(
                member.Name,
                member.Name,
                member.Kind,
                member.Description,
                FormatGenericName(member.Name, member.GenericArity)));
        }

        PackageTypeCompletion? type = null;
        if (catalog.TryFindType(expression, out var expressionType))
        {
            type = expressionType;
        }
        else
        {
            var declaredType = FindDeclaredType(editor.Text, expression, dotOffset);
            if (declaredType is not null && catalog.TryFindType(declaredType, out var variableType))
            {
                type = variableType;
            }
        }

        var declaredMembers = type is null
            ? Enumerable.Empty<CompletionEntry>()
            : type.Members
                .Where(member => expression == type.Name || expression == type.FullName
                    ? member.IsStatic
                    : !member.IsStatic)
                .Select(member => new CompletionEntry(
                member.Name,
                member.Name,
                member.Kind,
                $"{type.FullName}.{member.Name}",
                FormatGenericName(member.Name, member.GenericArity)));
        return declaredMembers;
    }

    private static IEnumerable<CompletionEntry> GetSnippetEntries(TextEditor editor)
    {
        var context = CSharpScriptSymbolCatalog.GetSnippetContext(
            editor.Text,
            editor.TextArea.Caret.Offset);
        if (context is null)
        {
            return [];
        }

        return
        [
            new CompletionEntry(
                "prop",
                "prop",
                "snippet",
                "Declare an auto-implemented property.",
                "prop  property",
                90,
                CompletePropertySnippet),
            new CompletionEntry(
                "ctor",
                "ctor",
                "snippet",
                $"Declare a constructor for {context.TypeName}.",
                $"ctor  {context.TypeName}()",
                90,
                (textArea, segment) => CompleteConstructorSnippet(textArea, segment, context.TypeName))
        ];
    }

    private static void CompletePropertySnippet(TextArea textArea, ISegment segment)
    {
        const string insertion = "public int PropertyName { get; set; }";
        textArea.Document.Replace(segment, insertion);
        var editor = FindEditor(textArea);
        editor?.Select(segment.Offset + "public ".Length, "int".Length);
    }

    private static void CompleteConstructorSnippet(TextArea textArea, ISegment segment, string typeName)
    {
        var line = textArea.Document.GetLineByOffset(segment.Offset);
        var indentation = textArea.Document.GetText(line.Offset, segment.Offset - line.Offset);
        if (indentation.Any(character => !char.IsWhiteSpace(character)))
        {
            indentation = string.Empty;
        }

        var newLine = Environment.NewLine;
        var bodyIndentation = indentation + "    ";
        var insertion = $"public {typeName}(){newLine}{indentation}{{{newLine}{bodyIndentation}{newLine}{indentation}}}";
        textArea.Document.Replace(segment, insertion);
        var bodyMarker = $"{newLine}{bodyIndentation}{newLine}";
        textArea.Caret.Offset = segment.Offset + insertion.IndexOf(bodyMarker, StringComparison.Ordinal) +
                               newLine.Length + bodyIndentation.Length;
    }

    private static void CompleteTypeWithUsing(
        TextArea textArea,
        ISegment segment,
        string typeName,
        string typeNamespace)
    {
        var replacementOffset = segment.Offset;
        textArea.Document.Replace(segment, typeName);
        textArea.Caret.Offset = replacementOffset + typeName.Length;
        AddUsingDirective(textArea, typeNamespace);
    }

    private static string FormatGenericName(string name, int arity) => arity switch
    {
        <= 0 => name,
        1 => $"{name}<T>",
        _ => $"{name}<{string.Join(", ", Enumerable.Range(1, arity).Select(index => $"T{index}"))}>"
    };

    private static HashSet<string> GetImportedNamespaces(string text) =>
        Regex.Matches(text, @"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string ReadQualifiedIdentifierBefore(IDocument document, int offset)
    {
        var end = Math.Clamp(offset, 0, document.TextLength);
        var start = end;
        while (start > 0)
        {
            var character = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(character) && character is not '_' and not '.')
            {
                break;
            }

            start--;
        }

        return document.GetText(start, end - start).Trim('.');
    }

    private static string ReadIdentifierAt(IDocument document, int offset)
    {
        if (document.TextLength == 0)
        {
            return string.Empty;
        }

        var anchor = Math.Clamp(offset, 0, document.TextLength);
        if (anchor == document.TextLength || !IsIdentifierCharacter(document.GetCharAt(anchor)))
        {
            anchor--;
        }

        if (anchor < 0 || !IsIdentifierCharacter(document.GetCharAt(anchor)))
        {
            return string.Empty;
        }

        var start = anchor;
        while (start > 0 && IsIdentifierCharacter(document.GetCharAt(start - 1)))
        {
            start--;
        }

        var end = anchor + 1;
        while (end < document.TextLength && IsIdentifierCharacter(document.GetCharAt(end)))
        {
            end++;
        }

        return document.GetText(start, end - start);
    }

    private static string ReadQualifiedIdentifierAt(IDocument document, int offset)
    {
        if (document.TextLength == 0)
        {
            return string.Empty;
        }

        var anchor = Math.Clamp(offset, 0, document.TextLength - 1);
        if (!IsDebugExpressionCharacter(document.GetCharAt(anchor)) &&
            anchor > 0 && IsDebugExpressionCharacter(document.GetCharAt(anchor - 1)))
        {
            anchor--;
        }

        if (!IsDebugExpressionCharacter(document.GetCharAt(anchor)))
        {
            return string.Empty;
        }

        var start = anchor;
        while (start > 0 && IsDebugExpressionCharacter(document.GetCharAt(start - 1)))
        {
            start--;
        }

        var end = anchor + 1;
        while (end < document.TextLength && IsDebugExpressionCharacter(document.GetCharAt(end)))
        {
            end++;
        }

        return document.GetText(start, end - start).Trim('.');
    }

    private static bool IsDebugExpressionCharacter(char character) =>
        IsIdentifierCharacter(character) || character == '.';

    private static bool TryResolveDebugValue(
        ScriptDebugPause pause,
        string expression,
        out object? value)
    {
        value = null;
        var segments = expression.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !pause.Values.TryGetValue(segments[0], out value))
        {
            return false;
        }

        foreach (var segment in segments.Skip(1))
        {
            if (value is null)
            {
                return true;
            }

            var property = value.GetType().GetProperty(
                segment,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null || !property.CanRead || property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            try
            {
                value = property.GetValue(value);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

    private static string? FindDeclaredType(string text, string variableName, int beforeOffset)
    {
        if (variableName.Length == 0)
        {
            return null;
        }

        var precedingText = text[..Math.Clamp(beforeOffset, 0, text.Length)];
        var escapedName = Regex.Escape(variableName);
        var explicitMatches = Regex.Matches(
            precedingText,
            $@"\b([A-Za-z_][A-Za-z0-9_.]*(?:\s*<[^;=]+>)?)\s+{escapedName}\b");
        if (explicitMatches.Count > 0)
        {
            var candidate = explicitMatches[explicitMatches.Count - 1].Groups[1].Value.Trim();
            if (!string.Equals(candidate, "var", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        var inferredMatches = Regex.Matches(
            precedingText,
            $@"\bvar\s+{escapedName}\s*=\s*new\s+([A-Za-z_][A-Za-z0-9_.]*)");
        return inferredMatches.Count == 0 ? null : inferredMatches[inferredMatches.Count - 1].Groups[1].Value;
    }

    private static void ShowCompletion(
        TextEditor editor,
        IEnumerable<CompletionEntry> entries,
        int startOffset,
        bool automatic)
    {
        CloseCompletion(editor);

        var queryLength = Math.Max(0, editor.TextArea.Caret.Offset - startOffset);
        var query = queryLength == 0 ? string.Empty : editor.Document.GetText(startOffset, queryLength);
        var completionEntries = entries
            .Where(entry => !automatic || query.Length == 0 ||
                            entry.Text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Identity, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.Priority)
            .ThenBy(entry => entry.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (completionEntries.Length == 0)
        {
            return;
        }

        var window = new CompletionWindow(editor.TextArea)
        {
            StartOffset = startOffset,
            Width = 460,
            MaxHeight = 280,
            Background = PopupBackground,
            Foreground = PopupForeground,
            BorderBrush = PopupBorder,
            BorderThickness = new Thickness(1),
            CloseWhenCaretAtBeginning = false,
            Tag = new CompletionSession(automatic, completionEntries)
        };

        foreach (var entry in completionEntries)
        {
            window.CompletionList.CompletionData.Add(entry);
        }

        var listBox = window.CompletionList.ListBox;
        listBox.Background = PopupBackground;
        listBox.Foreground = PopupForeground;
        listBox.BorderThickness = new Thickness(0);
        listBox.FontFamily = new FontFamily("Cascadia Code, Consolas");
        listBox.FontSize = 12;
        listBox.Padding = new Thickness(2);

        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 5, 9, 5)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, PopupForeground));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Triggers.Add(new Trigger
        {
            Property = ListBoxItem.IsSelectedProperty,
            Value = true,
            Setters = { new Setter(Control.BackgroundProperty, PopupSelection) }
        });
        listBox.ItemContainerStyle = itemStyle;

        window.Closed += (_, _) => editor.ClearValue(CompletionWindowProperty);
        editor.SetValue(CompletionWindowProperty, window);
        window.Show();

        var length = editor.TextArea.Caret.Offset - startOffset;
        if (length > 0)
        {
            window.CompletionList.SelectItem(query);
            var preferred = completionEntries.FirstOrDefault(entry =>
                entry.Priority >= 100 && entry.Text.StartsWith(query, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                window.CompletionList.SelectedItem = preferred;
            }
        }
    }

    private static void RefreshCompletionSelection(TextEditor editor)
    {
        var window = GetCompletionWindow(editor);
        if (window is null || window.Tag is not CompletionSession { Automatic: true } session)
        {
            return;
        }

        var length = editor.TextArea.Caret.Offset - window.StartOffset;
        if (length <= 0)
        {
            return;
        }

        var query = editor.Document.GetText(window.StartOffset, length);
        var matches = session.Entries
            .Where(entry => entry.Text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            window.Close();
            return;
        }

        window.CompletionList.CompletionData.Clear();
        foreach (var match in matches)
        {
            window.CompletionList.CompletionData.Add(match);
        }

        window.CompletionList.SelectItem(query);
        window.CompletionList.SelectedItem = matches[0];
    }

    private static int FindWordStart(IDocument document, int offset)
    {
        var start = Math.Clamp(offset, 0, document.TextLength);
        while (start > 0)
        {
            var character = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                break;
            }

            start--;
        }

        return start;
    }

    private static TextEditor? FindEditor(DependencyObject source)
    {
        if (source.GetValue(OwnerEditorProperty) is TextEditor owner)
        {
            return owner;
        }

        DependencyObject? current = source;
        while (current is not null && current is not TextEditor)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return current as TextEditor;
    }

    private static CompletionWindow? GetCompletionWindow(DependencyObject editor) =>
        editor.GetValue(CompletionWindowProperty) as CompletionWindow;

    private static void CloseCompletion(DependencyObject editor) => GetCompletionWindow(editor)?.Close();

    private static void CloseQuickFixMenu(DependencyObject editor)
    {
        if (editor.GetValue(QuickFixMenuProperty) is ContextMenu menu)
        {
            menu.IsOpen = false;
            editor.ClearValue(QuickFixMenuProperty);
        }
    }

    private static void ShowQuickInfo(TextEditor editor, FrameworkElement content)
    {
        CloseQuickInfo(editor);
        var toolTip = new ToolTip
        {
            OverridesDefaultStyle = true,
            Template = QuickInfoToolTipTemplate,
            PlacementTarget = editor,
            Placement = PlacementMode.MousePoint,
            HorizontalOffset = 12,
            VerticalOffset = 18,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Content = content,
            StaysOpen = true
        };
        editor.SetValue(QuickInfoToolTipProperty, toolTip);
        toolTip.IsOpen = true;
    }

    private static void CloseQuickInfo(DependencyObject editor)
    {
        if (editor.GetValue(QuickInfoToolTipProperty) is ToolTip toolTip)
        {
            toolTip.IsOpen = false;
            editor.ClearValue(QuickInfoToolTipProperty);
        }
    }

    private static FrameworkElement CreateDiagnosticContent(SemanticDiagnostic diagnostic)
    {
        var severityBrush = diagnostic.Severity == "error" ? ErrorBrush : WarningBrush;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"{diagnostic.Id}  {diagnostic.Severity}",
            Foreground = severityBrush,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = diagnostic.Message,
            Foreground = PopupForeground,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620
        });
        return WrapQuickInfo(panel);
    }

    private static FrameworkElement CreateSymbolContent(SemanticQuickInfo quickInfo)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = quickInfo.Signature,
            Foreground = TypeBrush,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 680
        });
        if (quickInfo.Summary.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = quickInfo.Summary,
                Foreground = PopupForeground,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 680
            });
        }

        foreach (var parameter in quickInfo.Parameters)
        {
            var line = new TextBlock
            {
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 680
            };
            line.Inlines.Add(new System.Windows.Documents.Run(parameter.Name) { Foreground = VariableBrush });
            line.Inlines.Add(new System.Windows.Documents.Run($"  {parameter.Description}") { Foreground = MutedForeground });
            panel.Children.Add(line);
        }

        if (quickInfo.Overloads.Count > 1)
        {
            panel.Children.Add(new Border
            {
                BorderBrush = PopupBorder,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 10, 0, 6)
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"Overloads ({quickInfo.Overloads.Count})",
                Foreground = MutedForeground,
                FontWeight = FontWeights.SemiBold
            });
            foreach (var overload in quickInfo.Overloads.Take(12))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = overload,
                    Foreground = PopupForeground,
                    FontFamily = new FontFamily("Cascadia Code, Consolas"),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 680
                });
            }
        }

        return WrapQuickInfo(panel);
    }

    private static FrameworkElement CreateDebugValueContent(string expression, object? value)
    {
        var panel = new StackPanel();
        var type = value?.GetType();
        var header = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontWeight = FontWeights.SemiBold
        };
        header.Inlines.Add(new System.Windows.Documents.Run(expression) { Foreground = VariableBrush });
        header.Inlines.Add(new System.Windows.Documents.Run($"  {GetFriendlyTypeName(type)}") { Foreground = TypeBrush });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = FormatDebugValue(value),
            Foreground = PopupForeground,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            Margin = new Thickness(0, 7, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620
        });

        if (value is not null && !IsSimpleDebugValue(type!))
        {
            var properties = type!.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Take(20)
                .ToArray();
            if (properties.Length > 0)
            {
                panel.Children.Add(new Border
                {
                    BorderBrush = PopupBorder,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, 9, 0, 5)
                });
            }

            foreach (var property in properties)
            {
                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (Exception exception)
                {
                    propertyValue = $"<{exception.GetType().Name}>";
                }

                var row = new TextBlock
                {
                    FontFamily = new FontFamily("Cascadia Code, Consolas"),
                    Margin = new Thickness(0, 3, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 620
                };
                row.Inlines.Add(new System.Windows.Documents.Run(property.Name) { Foreground = MemberBrush });
                row.Inlines.Add(new System.Windows.Documents.Run($" = {FormatDebugValue(propertyValue)}") { Foreground = PopupForeground });
                panel.Children.Add(row);
            }
        }

        return WrapQuickInfo(panel);
    }

    private static bool IsSimpleDebugValue(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive || actualType.IsEnum || actualType == typeof(string) ||
               actualType == typeof(decimal) || actualType == typeof(DateTime) ||
               actualType == typeof(DateOnly) || actualType == typeof(TimeOnly) || actualType == typeof(Guid);
    }

    private static string GetFriendlyTypeName(Type? type) => type is null ? "null" : type.FullName ?? type.Name;

    private static string FormatDebugValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return $"\"{text}\"";
        }

        try
        {
            var result = value.ToString() ?? value.GetType().Name;
            return result.Length <= 240 ? result : result[..237] + "...";
        }
        catch
        {
            return $"<{value.GetType().Name}>";
        }
    }

    private static Border WrapQuickInfo(UIElement content) => new()
    {
        Background = PopupBackground,
        BorderBrush = PopupBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(12, 10, 12, 10),
        Child = content,
        MaxWidth = 720
    };

    private static ControlTemplate CreateQuickInfoToolTipTemplate()
    {
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        presenter.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        return new ControlTemplate(typeof(ToolTip))
        {
            VisualTree = presenter
        };
    }

    private static CompletionEntry Entry(string text, string kind, string description) =>
        new(text, text, kind, description);

    private static SolidColorBrush FrozenBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(string color, double thickness)
    {
        var pen = new Pen(FrozenBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private sealed class CompletionEntry(
        string text,
        string insertionText,
        string kind,
        string description,
        string? displayText = null,
        double? sortPriority = null,
        Action<TextArea, ISegment>? completion = null,
        string? identity = null) : ICompletionData
    {
        public ImageSource? Image => null;

        public string Text { get; } = text;

        public string InsertionText { get; } = insertionText;

        public string Identity { get; } = identity ?? $"{text}\0{insertionText}";

        private string DisplayText { get; } = displayText ?? text;

        public object Content => CreateContent();

        public object Description => description;

        public double Priority { get; } = sortPriority ?? (kind switch
        {
            "snippet" => 5,
            "modifier" => 5,
            "local" or "parameter" => 4,
            "property" or "method" or "extension" or "LINQ" => 3,
            "class" or "static class" or "interface" or "enum" or "struct" or "delegate" => 2,
            _ => 0
        });

        public bool ShouldCommit(char character) =>
            character == ' ' && (kind is "keyword" or "modifier");

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            if (completion is not null)
            {
                completion(textArea, completionSegment);
                return;
            }

            textArea.Document.Replace(completionSegment, InsertionText);
        }

        private FrameworkElement CreateContent()
        {
            var (glyph, brush) = GetAppearance(kind);
            var grid = new Grid { MinWidth = 400 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Width = 17,
                Height = 17,
                CornerRadius = new CornerRadius(2),
                Background = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = BadgeForeground,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grid.Children.Add(badge);

            var name = new TextBlock
            {
                Text = DisplayText,
                Margin = new Thickness(2, 0, 10, 0),
                Foreground = PopupForeground,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var kindLabel = new TextBlock
            {
                Text = kind,
                Foreground = brush,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(kindLabel, 2);
            grid.Children.Add(kindLabel);
            return grid;
        }

        private static (string Glyph, Brush Brush) GetAppearance(string entryKind) => entryKind switch
        {
            "local" => ("V", VariableBrush),
            "interface" => ("I", InterfaceBrush),
            "enum" => ("E", EnumBrush),
            "struct" => ("S", VariableBrush),
            "delegate" => ("D", MemberBrush),
            "class" or "static class" or "type" => ("C", TypeBrush),
            "method" or "extension" or "LINQ" => ("M", MemberBrush),
            "property" => ("P", EnumBrush),
            "quick fix" => ("+", QuickFixBrush),
            "keyword" or "modifier" => ("K", KeywordBrush),
            _ => ("•", MutedForeground)
        };
    }

    private sealed record CompletionSession(bool Automatic, IReadOnlyList<CompletionEntry> Entries);

    private sealed class EditorAnalysisState : IDisposable
    {
        private readonly TextEditor _editor;
        private readonly CSharpDiagnosticRenderer _renderer = new();
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource? _cancellation;
        private bool _disposed;

        public EditorAnalysisState(TextEditor editor)
        {
            _editor = editor;
            _timer = new DispatcherTimer(DispatcherPriority.Background, editor.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _timer.Tick += OnTimerTick;
            _editor.TextChanged += OnTextChanged;
            _editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        }

        public IReadOnlyList<SemanticDiagnostic> Diagnostics { get; private set; } = [];

        public void Schedule()
        {
            if (_disposed)
            {
                return;
            }

            _timer.Stop();
            _timer.Start();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _editor.TextChanged -= OnTextChanged;
            _editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
            _cancellation?.Cancel();
            _cancellation?.Dispose();
        }

        private void OnTextChanged(object? sender, EventArgs args) => Schedule();

        private void OnTimerTick(object? sender, EventArgs args)
        {
            _timer.Stop();
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            var cancellationToken = _cancellation.Token;
            var text = _editor.Text;
            var referencePaths = GetCompletionCatalog(_editor).ReferencePaths.ToArray();
            var scriptPath = GetScriptPath(_editor);
            IReadOnlyList<SemanticDiagnostic> diagnostics;
            try
            {
                diagnostics = await Task.Run(
                    () => CSharpSemanticCompletionService.GetDiagnostics(
                        text,
                        referencePaths,
                        cancellationToken,
                        scriptPath),
                    cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_disposed || cancellationToken.IsCancellationRequested || !string.Equals(text, _editor.Text, StringComparison.Ordinal))
            {
                return;
            }

            Diagnostics = diagnostics;
            _renderer.Update(diagnostics, _editor.TextArea.TextView);
        }
    }
}
