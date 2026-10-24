using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace MachineApplication.Entrance.Views;

public class XmlCodeEditor : TextEditor {
    public static readonly DependencyProperty BoundTextProperty = DependencyProperty.Register(
        nameof(BoundText), typeof(string), typeof(XmlCodeEditor),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnBoundTextChanged));

    private bool _synchronizing;
    private bool? _darkHighlighting;

    public string BoundText {
        get => (string)GetValue(BoundTextProperty);
        set => SetValue(BoundTextProperty, value);
    }

    public XmlCodeEditor() {
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;
        SearchPanel.Install(TextArea);
        Loaded += (_, _) => UpdateHighlighting();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == BackgroundProperty && IsLoaded) {
            UpdateHighlighting();
        }
    }

    private void UpdateHighlighting() {
        if (Background is not SolidColorBrush background) return;
        var color = background.Color;
        var dark = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B < 128;
        if (_darkHighlighting == dark) return;

        var assembly = typeof(HighlightingManager).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".XML-Mode.xshd", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = XmlReader.Create(stream);
        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        if (dark) {
            foreach (var namedColor in definition.NamedHighlightingColors) {
                var hex = namedColor.Name switch {
                    "Comment" => "#8FBC8F",
                    "CData" => "#DCDCAA",
                    "XmlTag" => "#569CD6",
                    "AttributeName" => "#9CDCFE",
                    "AttributeValue" => "#CE9178",
                    "Entity" => "#DCDCAA",
                    _ => "#C5A5E8"
                };
                namedColor.Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }

        SyntaxHighlighting = definition;
        _darkHighlighting = dark;
    }

    private static void OnBoundTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
        var editor = (XmlCodeEditor)sender;
        var text = args.NewValue as string ?? string.Empty;
        if (editor.Text == text) return;

        editor._synchronizing = true;
        try {
            editor.Text = text;
        }
        finally {
            editor._synchronizing = false;
        }
    }

    protected override void OnTextChanged(EventArgs e) {
        base.OnTextChanged(e);
        if (!_synchronizing) {
            SetCurrentValue(BoundTextProperty, Text);
        }
    }
}
