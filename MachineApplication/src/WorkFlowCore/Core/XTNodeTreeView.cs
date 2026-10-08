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
    private readonly Dictionary<string, bool> _expandedGroups = new(StringComparer.Ordinal);
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
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/WorkFlowCore;component/Themes/NodeLibrary.xaml", UriKind.Relative)
        });
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var root = new DockPanel();
        HintAssist.SetHint(_search, "搜索节点…");
        AutomationProperties.SetName(_search, "搜索节点");
        _search.SetResourceReference(StyleProperty, "NodeLibrary.Search");
        _tree.SetResourceReference(StyleProperty, "NodeLibrary.Tree");
        _tree.BorderThickness = new Thickness(0);
        _tree.Background = Brushes.Transparent;
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
        var selectedType = SelectedNodeType;
        var filtering = !string.IsNullOrWhiteSpace(_search.Text);
        _tree.Items.Clear();
        var groups = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);
        var counts = new Dictionary<string, TextBlock>(StringComparer.Ordinal);
        var totals = _types.GroupBy(GetCategory).ToDictionary(group => group.Key, group => group.Count());
        foreach (var type in _types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var attribute = type.GetCustomAttribute<XTNodeAttribute>();
            var path = attribute?.Path ?? "";
            var name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
            if (filtering && !$"{path}/{name}/{type.Name}/{attribute?.Description}".Contains(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)) continue;
            var category = GetCategory(type);
            if (!groups.TryGetValue(category, out var group))
            {
                var groupHeader = new Grid();
                groupHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                groupHeader.ColumnDefinitions.Add(new ColumnDefinition());
                groupHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var groupIcon = new PackIcon { Kind = PackIconKind.FolderOutline, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                groupIcon.SetResourceReference(ForegroundProperty, "NodeLibrary.Accent");
                groupHeader.Children.Add(groupIcon);
                var title = new TextBlock { Text = category, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(title, 1);
                groupHeader.Children.Add(title);
                var count = new TextBlock { FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                count.SetResourceReference(TextBlock.ForegroundProperty, "NodeLibrary.Muted");
                var badge = new Border { Child = count, MinWidth = 25, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.FromRgb(28, 37, 54)), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(badge, 2);
                groupHeader.Children.Add(badge);
                group = new TreeViewItem { Header = groupHeader, Tag = category, IsExpanded = filtering || _expandedGroups.GetValueOrDefault(category, true) };
                group.SetResourceReference(StyleProperty, "NodeLibrary.Item");
                group.Expanded += (_, _) => { if (string.IsNullOrWhiteSpace(_search.Text)) _expandedGroups[category] = true; };
                group.Collapsed += (_, _) => { if (string.IsNullOrWhiteSpace(_search.Text)) _expandedGroups[category] = false; };
                groups.Add(category, group);
                counts.Add(category, count);
                _tree.Items.Add(group);
            }
            // 目录只展示节点名称和类型，详细说明集中在右侧节点信息区，避免长描述撑高列表。
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new PackIcon
            {
                Kind = typeof(IEditorStartNode).IsAssignableFrom(type) ? PackIconKind.PlayCircleOutline : PackIconKind.CubeOutline,
                Width = 18, Height = 18, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(ForegroundProperty, typeof(IEditorStartNode).IsAssignableFrom(type) ? "Workflow.Green" : "NodeLibrary.Accent");
            header.Children.Add(icon);
            var caption = new StackPanel();
            caption.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
            var typeCaption = new TextBlock { Text = type.Name, FontSize = 10, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            typeCaption.SetResourceReference(TextBlock.ForegroundProperty, "NodeLibrary.Muted");
            caption.Children.Add(typeCaption);
            Grid.SetColumn(caption, 1);
            header.Children.Add(caption);
            var item = new TreeViewItem { Header = header, Tag = type, ToolTip = attribute?.Description, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            item.SetResourceReference(StyleProperty, "NodeLibrary.Item");
            AutomationProperties.SetName(item, name);
            group.Items.Add(item);
            if (type == selectedType) item.IsSelected = true;
        }
        foreach (var (category, group) in groups)
        {
            counts[category].Text = filtering ? $"{group.Items.Count} / {totals[category]}" : totals[category].ToString();
            counts[category].ToolTip = filtering ? $"匹配 {group.Items.Count} 个，共 {totals[category]} 个节点" : $"共 {totals[category]} 个节点";
            AutomationProperties.SetName(group, $"{category}，{counts[category].ToolTip}");
        }
        _empty.Visibility = _tree.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string GetCategory(Type type) => type.GetCustomAttribute<XTNodeAttribute>()?.Path
        .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "其他";
}
