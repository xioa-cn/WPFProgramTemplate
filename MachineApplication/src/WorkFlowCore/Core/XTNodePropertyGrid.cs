using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace ST.Library.UI.NodeEditor;

/// <summary>原生 WPF 节点属性面板，按标记属性生成编辑控件，错误在面板内展示。</summary>
public class XTNodePropertyGrid : UserControl
{
    private readonly StackPanel _items = new() { Margin = new Thickness(16) };
    private readonly StackPanel _nodeInformation = new();
    private readonly Border _informationFrame;
    private readonly ScrollViewer _informationScroll;
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 16, 12), Visibility = Visibility.Collapsed };
    private readonly ScrollViewer _scroll;
    public static readonly DependencyProperty XTNodeProperty = DependencyProperty.Register(nameof(XTNode), typeof(XTNode),
        typeof(XTNodePropertyGrid), new PropertyMetadata(null, OnNodeChanged));
    public XTNode? XTNode { get => (XTNode?)GetValue(XTNodeProperty); set => SetValue(XTNodeProperty, value); }
    public static readonly DependencyProperty ReadOnlyModelProperty = DependencyProperty.Register(nameof(ReadOnlyModel), typeof(bool),
        typeof(XTNodePropertyGrid), new PropertyMetadata(false, (owner, _) => ((XTNodePropertyGrid)owner).RefreshProperties()));
    public bool ReadOnlyModel { get => (bool)GetValue(ReadOnlyModelProperty); set => SetValue(ReadOnlyModelProperty, value); }
    public bool ShowTitle { get; set; } = true;
    public double ScrollOffset => _scroll.VerticalOffset;
    public event EventHandler? PropertyValueChanged;
    /// <summary>创建主题化属性区域，未选择节点时显示操作指引。</summary>
    public XTNodePropertyGrid()
    {
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.ValidationError");
        var root = new DockPanel();
        // 信息区固定放在分区标题下方，与参数列表分别滚动，长说明不会挤掉全部编辑空间。
        _informationScroll = new ScrollViewer
        {
            Content = _nodeInformation,
            MaxHeight = 210,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _informationFrame = new Border
        {
            Child = _informationScroll,
            Padding = new Thickness(16, 12, 16, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Visibility = Visibility.Collapsed
        };
        _informationFrame.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Separator.Background");
        DockPanel.SetDock(_informationFrame, Dock.Top);
        root.Children.Add(_informationFrame);
        DockPanel.SetDock(_error, Dock.Bottom);
        root.Children.Add(_error);
        _scroll = new ScrollViewer { Content = _items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(_scroll);
        Content = root;
        SizeChanged += OnPropertyPanelSizeChanged;
        RefreshProperties();
    }

    /// <summary>窄窗口上下分栏时限制信息区高度，为下面的参数编辑保留可用空间。</summary>
    private void OnPropertyPanelSizeChanged(object sender, SizeChangedEventArgs args)
    {
        _informationScroll.MaxHeight = Math.Min(210, Math.Max(48, args.NewSize.Height * .45 - 24));
    }

    /// <summary>从节点特性读取说明与作者资料，切换节点时清除旧信息并复位滚动位置。</summary>
    private void RefreshNodeInformation()
    {
        _nodeInformation.Children.Clear();
        _informationScroll.ScrollToTop();
        _informationFrame.Visibility = XTNode is null ? Visibility.Collapsed : Visibility.Visible;
        if (XTNode is null) return;

        if (ShowTitle)
            _nodeInformation.Children.Add(new TextBlock
            {
                Text = XTNode.Title, FontSize = 14, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
            });

        var metadata = XTNode.GetType().GetCustomAttribute<XTNodeAttribute>();
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var icon = new PackIcon { Kind = PackIconKind.InformationOutline, Width = 14, Height = 14, Margin = new Thickness(0, 0, 7, 0) };
        icon.SetResourceReference(ForegroundProperty, "Workflow.Accent");
        identity.Children.Add(icon);
        identity.Children.Add(new TextBlock { Text = "节点信息", FontSize = 11, FontWeight = FontWeights.SemiBold });
        _nodeInformation.Children.Add(identity);
        AddInformationRow("说明", string.IsNullOrWhiteSpace(metadata?.Description) ? "暂无节点说明" : metadata.Description);
        foreach (var output in XTNode.GetOutputOptions().Where(option => option != XTNodeOption.Empty && !string.IsNullOrWhiteSpace(option.Description)))
            AddInformationRow("输出", $"{output.Text}（{output.DataType?.Name}）\n{output.Description}");
        AddInformationRow("作者", metadata?.Author);
        AddInformationRow("邮箱", metadata?.Mail);
        AddInformationRow("主页", metadata?.Link);
    }

    /// <summary>按统一标签列展示元数据，空字段不占位，长文本在可用宽度内换行。</summary>
    private void AddInformationRow(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labelText = new TextBlock { Text = label, FontSize = 11 };
        labelText.SetResourceReference(TextBlock.ForegroundProperty, "Workflow.Muted");
        row.Children.Add(labelText);
        var content = new TextBlock
        {
            Text = value.Trim(), FontSize = 11, Opacity = .9,
            TextWrapping = TextWrapping.Wrap, ToolTip = value.Trim()
        };
        Grid.SetColumn(content, 1);
        row.Children.Add(content);
        _nodeInformation.Children.Add(row);
    }
    private static void OnNodeChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args) => ((XTNodePropertyGrid)owner).RefreshProperties();
    public void SetNode(XTNode? node) => XTNode = node;
    /// <summary>仅有校验错误时显示错误区域，避免空白占位挤压编辑空间。</summary>
    public void SetErrorMessage(string text)
    {
        _error.Text = text;
        _error.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>创建编辑器时不写入属性；用户提交失败时保留输入并显示错误。</summary>
    public void RefreshProperties()
    {
        _items.Children.Clear();
        SetErrorMessage("");
        RefreshNodeInformation();
        if (XTNode is null)
        {
            _items.Children.Add(new TextBlock { Text = "尚未选择节点", FontSize = 14, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 16, 0, 8) });
            _items.Children.Add(new TextBlock { Text = "点击画布中的节点，即可在这里查看和修改参数。", FontSize = 12, Opacity = .65, TextWrapping = TextWrapping.Wrap });
            return;
        }
        AddPortSection("输入连接", XTNode.GetInputOptions().Where(option => option != XTNodeOption.Empty).ToArray());
        AddPortSection("输出连接", XTNode.GetOutputOptions().Where(option => option != XTNodeOption.Empty).ToArray());
        if (XTNode is IEditorCustomEditorNode customEditor)
        {
            var edit = new Button { Content = customEditor.EditorButtonText, IsEnabled = !ReadOnlyModel, Margin = new Thickness(0, 0, 0, 14) };
            edit.Click += (_, _) =>
            {
                try
                {
                    if (!customEditor.OpenEditor(Window.GetWindow(this))) return;
                    RefreshProperties();
                    PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception exception) { SetErrorMessage(exception.GetBaseException().Message); }
            };
            _items.Children.Add(edit);
        }
        _items.Children.Add(new TextBlock { Text = "参数配置", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 14) });
        foreach (var descriptor in XTNodePropertyDescriptor.Create(XTNode, this))
        {
            if (!XTNode.IsPropertyVisible(descriptor.PropertyInfo.Name)) continue;
            var group = new StackPanel { Margin = new Thickness(0, 0, 0, 16), ToolTip = descriptor.Description };
            group.Children.Add(new TextBlock { Text = descriptor.Name, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            var propertyType = descriptor.PropertyInfo.PropertyType;
            if (propertyType == typeof(bool))
            {
                var check = new CheckBox { Content = "启用", FontSize = 12, IsChecked = (bool?)descriptor.GetValue(null), IsEnabled = !descriptor.IsReadOnly };
                check.Click += (_, _) => Commit(descriptor, () => descriptor.SetValue(check.IsChecked == true));
                group.Children.Add(check);
            }
            else if (propertyType.IsEnum)
            {
                var combo = new ComboBox { ItemsSource = Enum.GetValues(propertyType), SelectedItem = descriptor.GetValue(null), IsEnabled = !descriptor.IsReadOnly };
                var restoringSelection = false;
                combo.SelectionChanged += (_, _) =>
                {
                    if (restoringSelection || combo.SelectedItem is null) return;
                    // 类型切换可能联动默认值和端口信息；成功后刷新，失败时恢复真实的枚举选项。
                    if (Commit(descriptor, () => descriptor.SetValue(combo.SelectedItem))) RefreshProperties();
                    else
                    {
                        restoringSelection = true;
                        try { combo.SelectedItem = descriptor.GetValue(null); }
                        finally { restoringSelection = false; }
                    }
                };
                group.Children.Add(combo);
            }
            else
            {
                var text = new TextBox { Text = descriptor.GetStringFromValue(), IsReadOnly = descriptor.IsReadOnly, MinWidth = 80, MinHeight = 36, FontSize = 12, Padding = new Thickness(9, 7, 9, 7) };
                var saved = text.Text;
                void Save()
                {
                    if (descriptor.IsReadOnly || text.Text == saved) return;
                    if (Commit(descriptor, () => descriptor.SetValue(text.Text))) saved = text.Text;
                }
                text.LostKeyboardFocus += (_, _) => Save();
                text.KeyDown += (_, args) => { if (args.Key == Key.Enter) { Save(); args.Handled = true; } };
                group.Children.Add(text);
            }
            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                var description = new TextBlock { Text = descriptor.Description, FontSize = 12, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap };
                description.SetResourceReference(TextBlock.ForegroundProperty, "Workflow.Muted");
                group.Children.Add(description);
            }
            _items.Children.Add(group);
        }
    }
    /// <summary>显示选中节点真实的端口定义，不创建示例连接或改变流程数据。</summary>
    private void AddPortSection(string title, XTNodeOption[] options)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        section.Children.Add(new TextBlock { Text = $"{title}  ·  {options.Length}", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        if (options.Length == 0)
        {
            var empty = new TextBlock { Text = "无输入端口", FontSize = 11, Margin = new Thickness(10, 0, 0, 0) };
            empty.Text = title == "输入连接" ? "无输入端口" : "无输出端口";
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Workflow.Muted");
            section.Children.Add(empty);
        }
        foreach (var option in options)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(Border.BackgroundProperty, option.IsInput ? "Workflow.Accent" : "Workflow.Green");
            row.Children.Add(dot);
            var type = new TextBlock { Text = option.DataType?.Name ?? "任意", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), MaxWidth = 100, TextTrimming = TextTrimming.CharacterEllipsis };
            type.SetResourceReference(TextBlock.ForegroundProperty, "Workflow.Muted");
            DockPanel.SetDock(type, Dock.Right);
            row.Children.Add(type);
            row.Children.Add(new TextBlock { Text = option.Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            var frame = new Border { Child = row, Padding = new Thickness(10, 8, 10, 3), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 3) };
            frame.SetResourceReference(Border.BackgroundProperty, "Workflow.Surface");
            section.Children.Add(frame);
        }
        _items.Children.Add(section);
    }

    /// <summary>应用属性修改并发布通知，失败时保留输入并显示验证信息。</summary>
    private bool Commit(XTNodePropertyDescriptor descriptor, Action apply)
    {
        try { apply(); SetErrorMessage(""); PropertyValueChanged?.Invoke(this, EventArgs.Empty); return true; }
        catch (Exception exception) { descriptor.OnSetValueError(exception); return false; }
    }
}
