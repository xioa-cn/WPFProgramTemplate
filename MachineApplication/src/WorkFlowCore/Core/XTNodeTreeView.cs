using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace ST.Library.UI.NodeEditor;

/// <summary>WPF 节点目录，支持按特性路径分组、搜索、双击创建和拖放到画布。</summary>
public class XTNodeTreeView : UserControl
{
    private readonly HashSet<Type> _types = [];
    private readonly TreeView _tree = new();
    private readonly TextBox _search = new() { Margin = new Thickness(12, 12, 12, 8), ToolTip = "按节点名称或说明搜索", FontSize = 12, Padding = new Thickness(9, 7, 9, 7) };
    private readonly TextBlock _empty = new() { Text = "没有匹配的节点\n试试其他名称或关键词", Margin = new Thickness(16, 20, 16, 0), FontSize = 12, Opacity = .65, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private Point _dragStart;
    public event EventHandler<Type>? NodeRequested;
    public event EventHandler<Type>? NodeTypeAdded;
    public Type? SelectedNodeType => (_tree.SelectedItem as TreeViewItem)?.Tag as Type;
    /// <summary>创建带搜索提示的节点库，沿用目录选择、双击和拖放交互。</summary>
    public XTNodeTreeView()
    {
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var root = new DockPanel();
        HintAssist.SetHint(_search, "搜索节点…");
        AutomationProperties.SetName(_search, "搜索节点");
        _tree.BorderThickness = new Thickness(0);
        _tree.Background = Brushes.Transparent;
        _tree.Padding = new Thickness(4, 0, 4, 8);
        _tree.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(_tree, ScrollBarVisibility.Disabled);
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);
        DockPanel.SetDock(_empty, Dock.Top);
        root.Children.Add(_empty);
        root.Children.Add(_tree);
        Content = root;
        _search.TextChanged += (_, _) => Rebuild();
        _tree.MouseDoubleClick += (_, _) => { if (SelectedNodeType is { } type) NodeRequested?.Invoke(this, type); };
        _tree.PreviewMouseLeftButtonDown += (_, args) => _dragStart = args.GetPosition(_tree);
        _tree.MouseMove += (_, args) =>
        {
            if (args.LeftButton != MouseButtonState.Pressed || SelectedNodeType is not { } type) return;
            var delta = args.GetPosition(_tree) - _dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            DragDrop.DoDragDrop(_tree, new DataObject(typeof(Type), type), DragDropEffects.Copy);
        };
    }
    /// <summary>设置搜索关键词并即时筛选节点目录。</summary>
    public void Search(string text) => _search.Text = text;
    /// <summary>登记节点类型，忽略抽象类型和重复登记。</summary>
    public bool AddNode(Type type)
    {
        if (!typeof(XTNode).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) is null) return false;
        if (!_types.Add(type)) return false;
        Rebuild();
        NodeTypeAdded?.Invoke(this, type);
        return true;
    }
    /// <summary>从程序集加载带节点目录特性的可创建类型。</summary>
    public int LoadAssembly(string file)
    {
        var count = 0;
        foreach (var type in XTNodeEditor.LoadableTypes(Assembly.LoadFrom(System.IO.Path.GetFullPath(file))))
            if (type.GetCustomAttribute<XTNodeAttribute>() is not null && AddNode(type)) count++;
        return count;
    }
    /// <summary>清空节点目录。</summary>
    public void Clear() { _types.Clear(); Rebuild(); }
    /// <summary>移除指定节点类型并刷新目录。</summary>
    public bool RemoveNode(Type type) { var removed = _types.Remove(type); Rebuild(); return removed; }
    /// <summary>返回当前登记的节点类型。</summary>
    public Type[] GetTypes() => _types.ToArray();
    /// <summary>按分类重建目录；显示名称只影响界面，不修改持久化使用的类型标识。</summary>
    private void Rebuild()
    {
        _tree.Items.Clear();
        foreach (var type in _types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var attribute = type.GetCustomAttribute<XTNodeAttribute>();
            var path = attribute?.Path ?? "";
            var name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
            if (!string.IsNullOrWhiteSpace(_search.Text) && !$"{path}/{name}/{type.Name}/{attribute?.Description}".Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase)) continue;
            ItemCollection items = _tree.Items;
            foreach (var segment in path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            {
                var group = items.OfType<TreeViewItem>().FirstOrDefault(item => item.Tag is string category && category == segment);
                if (group is null)
                {
                    var groupHeader = new Border { Padding = new Thickness(8, 7, 8, 7), CornerRadius = new CornerRadius(3) };
                    groupHeader.SetResourceReference(Border.BackgroundProperty, "Workflow.Surface");
                    var groupCaption = new StackPanel { Orientation = Orientation.Horizontal };
                    var groupIcon = new PackIcon { Kind = PackIconKind.FolderOutline, Width = 14, Height = 14, Margin = new Thickness(0, 0, 7, 0) };
                    groupIcon.SetResourceReference(ForegroundProperty, "Workflow.Accent");
                    groupCaption.Children.Add(groupIcon);
                    groupCaption.Children.Add(new TextBlock { Text = segment, FontSize = 12 });
                    groupHeader.Child = groupCaption;
                    group = new TreeViewItem { Header = groupHeader, Tag = segment, IsExpanded = true, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                    items.Add(group);
                }
                items = group.Items;
            }
            // 目录只展示节点名称和类型，详细说明集中在右侧节点信息区，避免长描述撑高列表。
            var header = new Grid { Margin = new Thickness(0, 5, 8, 5) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new PackIcon
            {
                Kind = typeof(IEditorStartNode).IsAssignableFrom(type) ? PackIconKind.PlayCircleOutline : PackIconKind.CubeOutline,
                Width = 22, Height = 22, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Top
            };
            icon.SetResourceReference(ForegroundProperty, typeof(IEditorStartNode).IsAssignableFrom(type) ? "Workflow.Green" : "Workflow.Accent");
            header.Children.Add(icon);
            var caption = new StackPanel();
            caption.Children.Add(new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium });
            caption.Children.Add(new TextBlock { Text = type.Name, FontSize = 10, Opacity = .6, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(caption, 1);
            header.Children.Add(caption);
            var item = new TreeViewItem { Header = header, Tag = type, ToolTip = attribute?.Description, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(item, name);
            items.Add(item);
        }
        _empty.Visibility = _tree.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
