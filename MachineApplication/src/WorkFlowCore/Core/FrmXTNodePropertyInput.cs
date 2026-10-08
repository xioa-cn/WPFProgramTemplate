using System.Windows;
using System.Windows.Controls;
using ST.Library.UI.NodeEditor;

namespace ST.Library.UI;

/// <summary>原生 WPF 文本属性编辑窗口</summary>
internal class FrmXTNodePropertyInput : Window
{
    public FrmXTNodePropertyInput(XTNodePropertyDescriptor descriptor)
    {
        Title = descriptor.Name;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = descriptor.Control is null ? null : GetWindow(descriptor.Control);
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var panel = new StackPanel { Margin = new Thickness(20) };
        var input = new TextBox
        {
            Text = descriptor.GetStringFromValue(), IsReadOnly = descriptor.IsReadOnly,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        error.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.ValidationError");
        var buttons = new StackPanel
            { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button
        {
            Content = "确定", IsDefault = true, IsEnabled = !descriptor.IsReadOnly, Margin = new Thickness(0, 0, 8, 0)
        };
        var cancel = new Button { Content = "取消", IsCancel = true };
        save.Click += (_, _) =>
        {
            try
            {
                descriptor.SetValue(input.Text);
                descriptor.Invalidate();
                DialogResult = true;
            }
            catch (Exception exception)
            {
                error.Text = exception.GetBaseException().Message;
            }
        };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        panel.Children.Add(input);
        panel.Children.Add(error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
    }
}