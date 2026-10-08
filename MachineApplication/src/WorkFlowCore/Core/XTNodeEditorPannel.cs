using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace ST.Library.UI.NodeEditor;

/// <summary>节点编辑工作台：宽屏三栏分区，窄屏将节点库和属性合并到同一侧栏。</summary>
public class XTNodeEditorPannel : UserControl
{
    private readonly Grid _root = new();
    private readonly Border _libraryFrame;
    private readonly Border _canvasFrame;
    private readonly Border _propertyFrame;
    private readonly TextBlock _nodeCount = new() { FontSize = 12, Opacity = .65, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = .7, TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Collapsed };
    private readonly Button _scaleButton;
    private readonly Dictionary<ConnectionStatus, string> _statusText = [];
    private bool _leftLayout = true;
    private bool _compact = true;

    public XTNodeEditor Editor { get; } = new();
    public XTNodeTreeView TreeView { get; } = new();
    public XTNodePropertyGrid PropertyGrid { get; } = new();
    public bool ShowScale { get; set; } = true;
    public bool ShowConnectionStatus { get; set; } = true;
    public bool LeftLayout
    {
        get => _leftLayout;
        set { if (_leftLayout == value) return; _leftLayout = value; BuildLayout(); }
    }

    /// <summary>创建带分区标题的工作台，并连接节点选择、数量和缩放反馈。</summary>
    public XTNodeEditorPannel()
    {
        MinWidth = 360;
        MinHeight = 260;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Background");
        SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        Content = _root;
        Editor.ShowBorder = false;
        Editor.GridColor = Color.FromArgb(12, 155, 177, 214);
        Editor.Background = new RadialGradientBrush(Color.FromRgb(36, 46, 65), Color.FromRgb(24, 32, 48))
        {
            Center = new Point(.45, .35), GradientOrigin = new Point(.45, .35), RadiusX = .85, RadiusY = .9
        };
        Editor.HighLineColor = Color.FromRgb(172, 140, 255);

        var zoom = new StackPanel { Orientation = Orientation.Horizontal };
        zoom.Children.Add(CreateToolButton("−", "缩小画布", () => ZoomCanvas(Editor.CanvasScale / 1.15f)));
        _scaleButton = CreateToolButton("100%", "恢复 100% 缩放", () => ZoomCanvas(1));
        _scaleButton.MinWidth = 52;
        zoom.Children.Add(_scaleButton);
        zoom.Children.Add(CreateToolButton("+", "放大画布", () => ZoomCanvas(Editor.CanvasScale * 1.15f)));
        zoom.Children.Add(CreateToolButton("适应", "缩放并居中显示全部节点", FitCanvas));

        var canvasBody = new DockPanel();
        var hint = new TextBlock
        {
            Text = "空白处拖动平移 · Ctrl + 滚轮缩放 · 选中连线后 Delete 删除",
            ToolTip = "空白处按住左键拖动画布；节点上按住左键移动节点；拖动端口连接；单击连线后按 Delete 删除，或右击连线选择删除；右击端口断开全部连接；Ctrl + 滚轮缩放；也支持中键或空格 + 左键平移；Delete 删除选中的节点或连线。",
            FontSize = 11, Opacity = .6, Margin = new Thickness(12, 8, 12, 8),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        canvasBody.Children.Add(hint);
        canvasBody.Children.Add(Editor);

        _libraryFrame = CreateSection("节点库", "双击添加，或拖入画布", TreeView);
        _canvasFrame = CreateSection("流程画布", null, canvasBody, zoom);
        _propertyFrame = CreateSection("节点设置", "说明 · 作者资料 · 参数", PropertyGrid);
        BuildLayout();

        TreeView.NodeTypeAdded += (_, type) => Editor.RegisterNodeType(type);
        TreeView.NodeRequested += (_, type) =>
        {
            var node = (XTNode)Activator.CreateInstance(type)!;
            node.Location = Editor.ControlToCanvas(new Point(Editor.ActualWidth / 2, Editor.ActualHeight / 2));
            foreach (var selected in Editor.GetSelectedNode()) selected.IsSelected = false;
            Editor.Nodes.Add(node);
            Editor.SetActiveNode(node);
            node.IsSelected = true;
        };
        Editor.ActiveChanged += (_, _) => PropertyGrid.SetNode(Editor.ActiveNode);
        Editor.CanvasScaled += (_, _) => UpdateCanvasStatus();
        Editor.NodeAdded += (_, _) => UpdateCanvasStatus();
        Editor.NodeRemoved += (_, _) => UpdateCanvasStatus();
        Editor.OptionConnected += (_, args) => SetStatus(args.Status);
        Editor.OptionDisConnected += (_, args) => SetStatus(args.Status);
        // 加载文件直接恢复视口而不触发缩放事件，重新布局后仍需同步比例显示。
        Editor.LayoutUpdated += (_, _) => UpdateCanvasStatus();
        SizeChanged += OnWorkbenchSizeChanged;
        UpdateCanvasStatus();
    }

    /// <summary>创建主题卡片和固定分区标题，内部内容随剩余空间伸展。</summary>
    private Border CreateSection(string title, string? subtitle, UIElement content, UIElement? tools = null)
    {
        var root = new DockPanel();
        var header = new DockPanel { Margin = new Thickness(12, 9, 12, 9), LastChildFill = true };
        if (tools is not null)
        {
            DockPanel.SetDock(tools, Dock.Right);
            header.Children.Add(tools);
        }
        var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        caption.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold });
        if (subtitle is not null)
            caption.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Opacity = .6, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        else
            caption.Children.Add(_nodeCount);
        header.Children.Add(caption);
        var headerBorder = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) };
        headerBorder.SetResourceReference(Border.BackgroundProperty, "Workflow.Header");
        headerBorder.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Separator.Background");
        DockPanel.SetDock(headerBorder, Dock.Top);
        root.Children.Add(headerBorder);
        root.Children.Add(content);
        var frame = new Border { Child = root, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Padding = new Thickness(1, 1, 1, 5) };
        frame.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        frame.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Separator.Background");
        return frame;
    }

    /// <summary>创建可通过键盘聚焦的缩放按钮，并提供读屏名称。</summary>
    private Button CreateToolButton(string text, string description, Action action)
    {
        var button = new Button { Content = text, ToolTip = description, Height = 28, MinWidth = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 12 };
        button.SetResourceReference(StyleProperty, "Workflow.ToolButton");
        button.SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        ButtonAssist.SetCornerRadius(button, new CornerRadius(6));
        AutomationProperties.SetName(button, description);
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>以画布中心为锚点缩放，不改变当前关注区域。</summary>
    private void ZoomCanvas(float scale) => Editor.ScaleCanvas(scale, (float)Editor.ActualWidth / 2, (float)Editor.ActualHeight / 2);

    /// <summary>按实际节点包围盒适应画布，留出光晕和端口边距，不修改节点布局。</summary>
    private void FitCanvas()
    {
        var bounds = Editor.CanvasValidBounds;
        if (bounds.IsEmpty || Editor.ActualWidth <= 80 || Editor.ActualHeight <= 80) return;
        var scale = (float)Math.Clamp(Math.Min((Editor.ActualWidth - 80) / bounds.Width, (Editor.ActualHeight - 80) / bounds.Height), .2, 1.2);
        Editor.ScaleCanvas(scale, 0, 0);
        Editor.MoveCanvas((float)(Editor.ActualWidth / 2 - (bounds.X + bounds.Width / 2) * scale),
            (float)(Editor.ActualHeight / 2 - (bounds.Y + bounds.Height / 2) * scale), false, CanvasMoveArgs.All);
    }

    /// <summary>仅跨越布局断点时重排分区，避免拖动窗口时反复创建视觉树。</summary>
    private void OnWorkbenchSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var compact = args.NewSize.Width < 980;
        if (compact == _compact) return;
        _compact = compact;
        BuildLayout();
    }

    /// <summary>重排同一组编辑控件，保留画布、属性输入和节点目录的当前状态。</summary>
    private void BuildLayout()
    {
        // 分区可能仍属于旧侧栏，必须先解除父容器关系再重新挂载。
        foreach (var frame in new[] { _libraryFrame, _canvasFrame, _propertyFrame })
            if (frame.Parent is Panel parent) parent.Children.Remove(frame);
        _root.Children.Clear();
        _root.ColumnDefinitions.Clear();
        _root.RowDefinitions.Clear();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (_compact)
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = _leftLayout ? new GridLength(200) : new GridLength(1, GridUnitType.Star), MinWidth = 150 });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = _leftLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(200), MinWidth = 150 });
            var side = new Grid();
            side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 90 });
            side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.3, GridUnitType.Star), MinHeight = 110 });
            AddCell(side, _libraryFrame, 0);
            Grid.SetRow(_propertyFrame, 2);
            AddCell(side, _propertyFrame, 0);
            var splitter = CreateSplitter(false);
            Grid.SetRow(splitter, 1);
            side.Children.Add(splitter);
            AddCell(_root, side, _leftLayout ? 0 : 2);
            AddCell(_root, _canvasFrame, _leftLayout ? 2 : 0);
            AddCell(_root, CreateSplitter(true), 1);
        }
        else
        {
            foreach (var width in new[] { new GridLength(_leftLayout ? 220 : 290), new GridLength(8), new GridLength(1, GridUnitType.Star), new GridLength(8), new GridLength(_leftLayout ? 290 : 220) })
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            _root.ColumnDefinitions[0].MinWidth = 180;
            _root.ColumnDefinitions[2].MinWidth = 280;
            _root.ColumnDefinitions[4].MinWidth = 180;
            Grid.SetRow(_propertyFrame, 0);
            AddCell(_root, _libraryFrame, _leftLayout ? 0 : 4);
            AddCell(_root, _canvasFrame, 2);
            AddCell(_root, _propertyFrame, _leftLayout ? 4 : 0);
            AddCell(_root, CreateSplitter(true), 1);
            AddCell(_root, CreateSplitter(true), 3);
        }
        _status.Margin = new Thickness(4, 7, 4, 0);
        Grid.SetRow(_status, 1);
        Grid.SetColumnSpan(_status, _root.ColumnDefinitions.Count);
        _root.Children.Add(_status);
    }

    /// <summary>将分区放入指定列，不重建分区内部控件。</summary>
    private static void AddCell(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    /// <summary>使用透明拖拽区域替代粗分隔线，保留面板尺寸调整能力。</summary>
    private static GridSplitter CreateSplitter(bool vertical) => new()
    {
        Background = Brushes.Transparent,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
        ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS,
        ToolTip = "拖动调整面板大小"
    };

    /// <summary>同步节点数量与缩放显示；文本相同时不重复触发布局。</summary>
    private void UpdateCanvasStatus()
    {
        var count = $"{Editor.Nodes.Count} 个节点";
        if (_nodeCount.Text != count) _nodeCount.Text = count;
        var scale = $"{Editor.CanvasScale:P0}";
        if (!Equals(_scaleButton.Content, scale)) _scaleButton.Content = scale;
        _scaleButton.Visibility = ShowScale ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>以中文展示连接反馈，同时允许调用方覆盖提示文本。</summary>
    private void SetStatus(ConnectionStatus status)
    {
        if (!ShowConnectionStatus) return;
        _status.Visibility = Visibility.Visible;
        _status.Text = _statusText.GetValueOrDefault(status, status switch
        {
            ConnectionStatus.Connected => "连接已建立",
            ConnectionStatus.DisConnected => "连接已断开",
            _ => "无法连接这两个端口，请检查方向、数据类型及连接限制。"
        });
    }

    /// <summary>登记可用节点类型，同步更新目录和画布反序列化白名单。</summary>
    public bool AddXTNode(Type type) => TreeView.AddNode(type);

    /// <summary>从指定程序集导入节点类型。</summary>
    public int LoadAssembly(string file) => TreeView.LoadAssembly(file);

    /// <summary>覆盖连接反馈文案，并返回此前配置的文本。</summary>
    public string SetConnectionStatusText(ConnectionStatus status, string text)
    {
        var previous = _statusText.GetValueOrDefault(status, status.ToString());
        _statusText[status] = text;
        return previous;
    }
}
