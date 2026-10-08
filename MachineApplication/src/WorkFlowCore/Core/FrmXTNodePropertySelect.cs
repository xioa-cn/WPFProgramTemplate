using System.Windows;
using System.Windows.Controls;

namespace ST.Library.UI.NodeEditor;

/// <summary>布尔及枚举选择窗口，提交异常保留在窗口内显示。</summary>
internal class FrmXTNodePropertySelect : Window
{
    public FrmXTNodePropertySelect(XTNodePropertyDescriptor descriptor)
    {
        Title = descriptor.Name;
        Width = 320;
        Height = 360;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = descriptor.Control is null ? null : GetWindow(descriptor.Control);
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var panel = new DockPanel { Margin = new Thickness(16) };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        error.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.ValidationError");
        DockPanel.SetDock(error, Dock.Bottom);
        panel.Children.Add(error);
        var confirm = new Button
        {
            Content = "确定", IsDefault = true, Margin = new Thickness(0, 10, 0, 0), IsEnabled = !descriptor.IsReadOnly
        };
        DockPanel.SetDock(confirm, Dock.Bottom);
        panel.Children.Add(confirm);
        var type = descriptor.PropertyInfo.PropertyType;
        var items = new ListBox
        {
            ItemsSource = descriptor.GetSelectItems(type == typeof(bool) ? new object[] { false, true } : Enum.GetValues(type).Cast<object>()),
            DisplayMemberPath = "Text", SelectedValuePath = "Value",
            SelectedValue = descriptor.GetValue(null)
        };
        panel.Children.Add(items);
        confirm.Click += (_, _) =>
        {
            if (items.SelectedValue is null) return;
            try
            {
                descriptor.SetValue(items.SelectedValue);
                descriptor.Invalidate();
                DialogResult = true;
            }
            catch (Exception exception)
            {
                error.Text = exception.GetBaseException().Message;
            }
        };
        Content = panel;
    }
}
