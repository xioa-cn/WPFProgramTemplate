using System.Windows;
using ICSharpCode.AvalonEdit;

namespace CsxPad.Wpf.Helpers;

public static class AvalonEditorBinding
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(AvalonEditorBinding),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(AvalonEditorBinding));

    private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
        "IsHooked",
        typeof(bool),
        typeof(AvalonEditorBinding));

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextEditor editor)
        {
            return;
        }

        if (!(bool)editor.GetValue(IsHookedProperty))
        {
            editor.TextChanged += OnEditorTextChanged;
            editor.SetValue(IsHookedProperty, true);
        }

        if ((bool)editor.GetValue(IsUpdatingProperty))
        {
            return;
        }

        var text = args.NewValue as string ?? string.Empty;
        if (editor.Text == text)
        {
            return;
        }

        editor.SetValue(IsUpdatingProperty, true);
        editor.Text = text;
        editor.SetValue(IsUpdatingProperty, false);
    }

    private static void OnEditorTextChanged(object? sender, EventArgs args)
    {
        if (sender is not TextEditor editor || (bool)editor.GetValue(IsUpdatingProperty))
        {
            return;
        }

        editor.SetValue(IsUpdatingProperty, true);
        editor.SetCurrentValue(TextProperty, editor.Text);
        editor.SetValue(IsUpdatingProperty, false);
    }
}
