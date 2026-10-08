using System.Windows;
using System.Windows.Controls;

namespace ST.Library.UI.NodeEditor;

/// <summary>节点预览窗口使用独立节点实例，不占用编辑器中的现有节点。</summary>
internal class FrmNodePreviewPanel : Window
{
    public FrmNodePreviewPanel(Type nodeType)
    {
        Title = nodeType.Name;
        Width = 640;
        Height = 420;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var editor = new XTNodeEditor();
        var properties = new XTNodePropertyGrid { ReadOnlyModel = true };
        var root = new Grid();
        root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(240) });
        root.Children.Add(editor);
        Grid.SetColumn(properties, 1);
        root.Children.Add(properties);
        var node = (XTNode)Activator.CreateInstance(nodeType)!;
        node.Location = new Point(40, 70);
        editor.Nodes.Add(node);
        properties.SetNode(node);
        Content = root;
    }
}