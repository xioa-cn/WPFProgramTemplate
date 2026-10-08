using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.IO;

namespace ST.Library.UI.NodeEditor;

/// <summary>WPF 节点编辑画布，负责绘制网格、节点、连线以及鼠标拖拽和缩放。</summary>
public partial class XTNodeEditor : Panel
{
    private readonly Canvas _surface = new();
    private readonly List<ConnectionInfo> _connections = [];
    private ConnectionInfo? _selectedConnection;
    private readonly Dictionary<Type, Color> _typeColors = [];
    private XTNode? _dragNode;
    private Point _dragStart;
    private Point _nodeStart;
    private XTNodeOption? _connectingOption;
    private Point _lastMouse;
    private bool _panning;
    private MouseButton _panButton;
    private Cursor? _cursorBeforePan;
    public XTNodeCollection Nodes { get; }
    public float CanvasOffsetX { get; private set; }
    public float CanvasOffsetY { get; private set; }
    public Point CanvasOffset => new(CanvasOffsetX, CanvasOffsetY);
    public float CanvasScale { get; private set; } = 1;

    /// <summary>画布统一端口方向；默认上入下出，关闭开关后改为左入右出。</summary>
    public static readonly DependencyProperty VerticalPortsProperty = DependencyProperty.Register(
        nameof(VerticalPorts), typeof(bool), typeof(XTNodeEditor),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPortOrientationChanged));

    public bool VerticalPorts
    {
        get => (bool)GetValue(VerticalPortsProperty);
        set => SetValue(VerticalPortsProperty, value);
    }

    public event EventHandler? PortOrientationChanged;

    /// <summary>切换时终止未完成的鼠标操作，重算端口和连线，不删除或重建已有连接。</summary>
    private static void OnPortOrientationChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var editor = (XTNodeEditor)owner;
        editor.ResetMouseInteraction();
        if (editor.IsMouseCaptured) editor.ReleaseMouseCapture();
        foreach (var node in editor.Nodes) node.BuildSize(true, true, true);
        editor.InvalidateVisual();
        editor.PortOrientationChanged?.Invoke(editor, EventArgs.Empty);
    }
    public float Curvature { get; set; } = .35f;
    public bool ShowMagnet { get; set; } = true;
    public bool ShowBorder { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool ShowLocation { get; set; } = true;
    public Color GridColor { get; set; } = Color.FromArgb(32, 128, 128, 128);
    public Color BorderColor { get; set; } = Colors.Gray;
    public Color BorderHoverColor { get; set; } = Colors.SteelBlue;
    public Color BorderSelectedColor { get; set; } = Colors.DodgerBlue;
    public Color BorderActiveColor { get; set; } = Colors.Orange;
    public Color MarkForeColor { get; set; } = Colors.White;
    public Color MarkBackColor { get; set; } = Colors.DimGray;
    public Color MagnetColor { get; set; } = Colors.Orange;
    public Color SelectedRectangleColor { get; set; } = Colors.DodgerBlue;
    public Color HighLineColor { get; set; } = Colors.Orange;
    public Color LocationForeColor { get; set; } = Colors.White;
    public Color LocationBackColor { get; set; } = Colors.DimGray;
    public Color UnknownTypeColor { get; set; } = Colors.Gray;
    public XTNode? ActiveNode { get; private set; }
    public XTNode? HoverNode { get; private set; }
    public IReadOnlyList<ConnectionInfo> Connections => _connections.AsReadOnly();
    public event EventHandler? ActiveChanged;
    public event EventHandler? SelectedChanged;
    public event EventHandler? HoverChanged;
    public event XTNodeEditorEventHandler? NodeAdded;
    public event XTNodeEditorEventHandler? NodeRemoved;
    public event EventHandler? CanvasMoved;
    public event EventHandler? CanvasScaled;
    public event XTNodeEditorOptionEventHandler? OptionConnected;
    public event XTNodeEditorOptionEventHandler? OptionConnecting;
    public event XTNodeEditorOptionEventHandler? OptionDisConnected;
    public event XTNodeEditorOptionEventHandler? OptionDisConnecting;

    public XTNodeEditor()
    {
        Focusable = true;
        ClipToBounds = true;
        Background = Brushes.WhiteSmoke;
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Background");
        Children.Add(_surface);
        Nodes = new(this);
        MouseDown += EditorMouseDown;
        MouseMove += EditorMouseMove;
        MouseUp += EditorMouseUp;
        MouseWheel += EditorMouseWheel;
        KeyDown += EditorKeyDown;
        LostMouseCapture += (_, _) => { if (!IsMouseCaptured) ResetMouseInteraction(); };
        AllowDrop = true;
        Drop += OnNodeDrop;
        DragOver += (_, args) => { args.Effects = args.Data.GetDataPresent(typeof(Type)) ? DragDropEffects.Copy : DragDropEffects.None; args.Handled = true; };
    }
    public void AttachNode(XTNode node)
    {
        node.Owner = this;
        _surface.Children.Add(node);
        RegisterNodeType(node.GetType());
        NodeAdded?.Invoke(this, new(node));
        InvalidateVisual();
    }
    public void DetachNode(XTNode node)
    {
        foreach (var option in node.InputOptions.Concat(node.OutputOptions)) option.Detach();
        _connections.RemoveAll(connection => connection.Input.Owner == node || connection.Output.Owner == node);
        if (ReferenceEquals(ActiveNode, node)) SetActiveNode(null);
        _surface.Children.Remove(node);
        node.Owner = null;
        node.IsSelected = false;
        NodeRemoved?.Invoke(this, new(node));
        InvalidateVisual();
    }
    internal void NotifySelectionChanged() => SelectedChanged?.Invoke(this, EventArgs.Empty);
    internal void AddConnection(XTNodeOption output, XTNodeOption input)
    {
        if (!_connections.Any(connection => connection.Output == output && connection.Input == input))
            _connections.Add(new() { Output = output, Input = input });
        InvalidateVisual();
    }
    internal void RemoveConnection(XTNodeOption first, XTNodeOption second)
    {
        _connections.RemoveAll(connection =>
            (connection.Output == first && connection.Input == second) ||
            (connection.Output == second && connection.Input == first));
        if (_selectedConnection is { } selected && !_connections.Contains(selected))
            SetSelectedConnection(null);
        InvalidateVisual();
    }
    internal void OnOptionConnecting(XTNodeEditorOptionEventArgs args) => OptionConnecting?.Invoke(this, args);
    internal void OnOptionConnected(XTNodeEditorOptionEventArgs args) => OptionConnected?.Invoke(this, args);
    internal void OnOptionDisConnecting(XTNodeEditorOptionEventArgs args) => OptionDisConnecting?.Invoke(this, args);
    internal void OnOptionDisConnected(XTNodeEditorOptionEventArgs args) => OptionDisConnected?.Invoke(this, args);

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var tools = new DrawingTools(context, dpi);
        var background = Background ?? Brushes.Transparent;
        context.DrawRectangle(background, null, new Rect(RenderSize));
        context.PushTransform(new TranslateTransform(CanvasOffsetX, CanvasOffsetY));
        context.PushTransform(new ScaleTransform(CanvasScale, CanvasScale));
        if (ShowGrid) DrawGrid(context);
        foreach (var connection in _connections) DrawConnection(context, connection);
        if (_connectingOption is not null)
        {
            // 输入端开始拖线时反转起终点，使预览曲线仍沿输出到输入方向弯曲。
            var pointer = ToWorld(_lastMouse);
            DrawWire(context, _connectingOption.IsInput ? pointer : _connectingOption.Center,
                _connectingOption.IsInput ? _connectingOption.Center : pointer, HighLineColor);
        }
        context.Pop();
        context.Pop();
        if (ShowBorder && ActualWidth >= 1 && ActualHeight >= 1)
            context.DrawRectangle(null, new Pen(NodeDrawing.Brush(this, "MaterialDesign.Brush.Separator.Background", BorderColor), 1),
                new Rect(.5, .5, ActualWidth - 1, ActualHeight - 1));
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        _surface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 480 : availableSize.Height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        _surface.Arrange(new Rect(finalSize));
        UpdateTransform();
        return finalSize;
    }
    private void UpdateTransform()
    {
        _surface.RenderTransform = new MatrixTransform(CanvasScale, 0, 0, CanvasScale, CanvasOffsetX, CanvasOffsetY);
        InvalidateVisual();
    }
    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(GridColor), 1 / Math.Max(CanvasScale, .01));
        const double step = 24;
        var left = -CanvasOffsetX / CanvasScale;
        var top = -CanvasOffsetY / CanvasScale;
        var right = (ActualWidth - CanvasOffsetX) / CanvasScale;
        var bottom = (ActualHeight - CanvasOffsetY) / CanvasScale;
        for (var x = Math.Floor(left / step) * step; x < right; x += step) context.DrawLine(pen, new(x, top), new(x, bottom));
        for (var y = Math.Floor(top / step) * step; y < bottom; y += step) context.DrawLine(pen, new(left, y), new(right, y));
    }
    /// <summary>连线继承输出端口或源节点颜色，使复杂画布中的数据流向更易辨认。</summary>
    private void DrawConnection(DrawingContext context, ConnectionInfo connection)
    {
        var color = connection.Output.DotColor.A > 0 ? connection.Output.DotColor : connection.Output.Owner?.GetAccentColor() ?? GetTypeColor(connection.Output.DataType);
        var selected = _selectedConnection is { } current && current.Equals(connection);
        DrawWire(context, connection.Output.Center, connection.Input.Center, selected ? HighLineColor : color, selected);
    }
    /// <summary>统一生成绘制和命中检测使用的曲线，兼容左右及上下端口布局。</summary>
    private PathGeometry CreateWireGeometry(Point start, Point end)
    {
        var distance = Math.Max(36, Math.Abs(VerticalPorts ? end.Y - start.Y : end.X - start.X) * Curvature);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = start, IsFilled = false };
        var firstControl = VerticalPorts ? new Point(start.X, start.Y + distance) : new Point(start.X + distance, start.Y);
        var secondControl = VerticalPorts ? new Point(end.X, end.Y - distance) : new Point(end.X - distance, end.Y);
        figure.Segments.Add(new BezierSegment(firstControl, secondControl, end, true));
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
    /// <summary>叠加连线光晕，选中时加粗高亮，让待删除的连线清晰可见。</summary>
    private void DrawWire(DrawingContext context, Point start, Point end, Color color, bool selected = false)
    {
        var geometry = CreateWireGeometry(start, end);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(16, color.R, color.G, color.B)), 8), geometry);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)), 4), geometry);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(color), selected ? 3 / CanvasScale : 1.4), geometry);
    }
    /// <summary>按绘制顺序逆序检测曲线，点击容差保持为屏幕六像素，不随缩放失效。</summary>
    private ConnectionInfo? HitConnection(Point point)
    {
        var hitPen = new Pen(Brushes.Black, 12 / CanvasScale);
        foreach (var connection in _connections.AsEnumerable().Reverse())
            if (CreateWireGeometry(connection.Output.Center, connection.Input.Center).StrokeContains(hitPen, point))
                return connection;
        return null;
    }
    /// <summary>连线与节点互斥选中，防止删除连线时误删先前选中的节点。</summary>
    private void SetSelectedConnection(ConnectionInfo? connection)
    {
        _selectedConnection = connection;
        if (connection is not null)
        {
            foreach (var node in Nodes) node.IsSelected = false;
            SetActiveNode(null);
        }
        InvalidateVisual();
    }
    /// <summary>沿用端口断开协议，保留锁定和取消检查，并通知页面记录未保存修改。</summary>
    private void DeleteConnection(ConnectionInfo connection)
    {
        if (_connections.Contains(connection)) connection.Output.DisConnectOption(connection.Input);
    }
    /// <summary>为单条连线提供删除菜单，避免通过端口一次断开所有分支。</summary>
    private void ShowConnectionMenu(ConnectionInfo connection)
    {
        var menu = new ContextMenu { PlacementTarget = this };
        var delete = new MenuItem
        {
            Header = "删除连线",
            InputGestureText = "Delete",
            IsEnabled = connection.Output.Owner?.LockOption != true && connection.Input.Owner?.LockOption != true
        };
        delete.Click += (_, _) => DeleteConnection(connection);
        menu.Items.Add(delete);
        menu.IsOpen = true;
    }
    private Point ToWorld(Point point) => new((point.X - CanvasOffsetX) / CanvasScale, (point.Y - CanvasOffsetY) / CanvasScale);
    private XTNode? HitNode(Point point) => Nodes.Reverse().FirstOrDefault(node => node.Rectangle.Contains(point));
    private XTNodeOption? HitOption(Point point)
    {
        foreach (var node in Nodes.Reverse())
            foreach (var option in node.InputOptions.Concat(node.OutputOptions))
                if (option != XTNodeOption.Empty && option.DotRectangle.Contains(point)) return option;
        return null;
    }
    /// <summary>区分画布平移、节点移动和端口连接；空白区域支持直接按住左键拖动。</summary>
    private void EditorMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (_panning || _dragNode is not null || _connectingOption is not null) return;
        if (args.OriginalSource is DependencyObject source)
        {
            for (var current = source; current is not null && current != this; current = VisualTreeHelper.GetParent(current))
                if (current is XTNodeControl) return;
        }
        Focus();
        var world = ToWorld(args.GetPosition(this));
        _lastMouse = args.GetPosition(this);
        if (args.ChangedButton == MouseButton.Middle || (args.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            BeginCanvasPan(args.ChangedButton);
            args.Handled = true;
            return;
        }
        var option = HitOption(world);
        if (args.ChangedButton == MouseButton.Right && option is not null)
        {
            option.DisConnectionAll(); args.Handled = true; return;
        }
        var node = HitNode(world);
        if (option is null && node is null && args.ChangedButton is MouseButton.Left or MouseButton.Right &&
            HitConnection(world) is { } connection)
        {
            SetSelectedConnection(connection);
            if (args.ChangedButton == MouseButton.Right) ShowConnectionMenu(connection);
            args.Handled = true;
            return;
        }
        if (args.ChangedButton != MouseButton.Left) return;
        SetSelectedConnection(null);
        if (option is not null) { _connectingOption = option; CaptureMouse(); return; }
        SetActiveNode(node);
        if (node is null)
        {
            foreach (var item in Nodes) item.IsSelected = false;
            BeginCanvasPan(MouseButton.Left);
            args.Handled = true;
            return;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) node.IsSelected = !node.IsSelected;
        else foreach (var item in Nodes) item.IsSelected = ReferenceEquals(item, node);
        _dragNode = node; _dragStart = world; _nodeStart = node.Location; CaptureMouse();
    }

    /// <summary>捕获鼠标后进入平移模式，使拖出控件边界时仍能连续移动画布。</summary>
    private void BeginCanvasPan(MouseButton button)
    {
        if (!CaptureMouse()) return;
        _panButton = button;
        _panning = true;
        _cursorBeforePan = Cursor;
        SetCurrentValue(CursorProperty, Cursors.SizeAll);
    }

    /// <summary>结束或中断鼠标操作，恢复光标并清除残留拖动状态。</summary>
    private void ResetMouseInteraction()
    {
        if (_panning) SetCurrentValue(CursorProperty, _cursorBeforePan);
        _panning = false;
        _dragNode = null;
        _connectingOption = null;
        InvalidateVisual();
    }

    /// <summary>平移按屏幕像素计算，节点拖动按画布坐标计算，避免缩放后移动速度失真。</summary>
    private void EditorMouseMove(object sender, MouseEventArgs args)
    {
        var screen = args.GetPosition(this);
        var world = ToWorld(screen);
        var hover = HitNode(world);
        if (!ReferenceEquals(HoverNode, hover)) { HoverNode = hover; HoverChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual(); }
        if (_panning)
        {
            CanvasOffsetX += (float)(screen.X - _lastMouse.X); CanvasOffsetY += (float)(screen.Y - _lastMouse.Y); _lastMouse = screen;
            CanvasMoved?.Invoke(this, EventArgs.Empty); UpdateTransform(); args.Handled = true; return;
        }
        if (_dragNode is not null && args.LeftButton == MouseButtonState.Pressed && !_dragNode.LockLocation)
        {
            _dragNode.Location = new(_nodeStart.X + (world.X - _dragStart.X), _nodeStart.Y + (world.Y - _dragStart.Y)); InvalidateVisual();
        }
        _lastMouse = screen;
        if (_connectingOption is not null) InvalidateVisual();
    }
    /// <summary>只由启动操作的鼠标键结束拖动，避免其他按键松开时意外中断平移。</summary>
    private void EditorMouseUp(object sender, MouseButtonEventArgs args)
    {
        if (_panning && args.ChangedButton != _panButton) return;
        if (!_panning && args.ChangedButton != MouseButton.Left) return;
        var interacting = _panning || _dragNode is not null || _connectingOption is not null;
        if (args.ChangedButton == MouseButton.Left && _connectingOption is not null)
        {
            var target = HitOption(ToWorld(args.GetPosition(this)));
            if (target is not null && target != _connectingOption) _connectingOption.ConnectOption(target);
        }
        ResetMouseInteraction();
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (interacting) args.Handled = true;
    }
    private void EditorMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        var before = ToWorld(args.GetPosition(this));
        CanvasScale = Math.Clamp(CanvasScale * (args.Delta > 0 ? 1.1f : .9f), .2f, 3f);
        var after = ToWorld(args.GetPosition(this));
        CanvasOffsetX += (float)((after.X - before.X) * CanvasScale); CanvasOffsetY += (float)((after.Y - before.Y) * CanvasScale);
        CanvasScaled?.Invoke(this, EventArgs.Empty); UpdateTransform(); args.Handled = true;
    }
    /// <summary>画布获得焦点时删除选中的连线或节点，不拦截属性输入框的删除键。</summary>
    private void EditorKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Delete && ReferenceEquals(Keyboard.FocusedElement, this))
        {
            if (_selectedConnection is { } connection) DeleteConnection(connection);
            else foreach (var node in Nodes.Where(node => node.IsSelected).ToArray()) Nodes.Remove(node);
            args.Handled = true;
        }
    }
    public XTNode? SetActiveNode(XTNode? node)
    {
        if (node is not null && node.Owner != this) throw new ArgumentException("节点不属于此编辑器。", nameof(node));
        if (ReferenceEquals(ActiveNode, node)) return node;
        if (ActiveNode is not null) ActiveNode.IsActive = false;
        ActiveNode = node;
        if (node is not null) node.IsActive = true;
        ActiveChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual();
        return node;
    }
    public Color GetTypeColor(Type? type) => type is not null && _typeColors.TryGetValue(type, out var color) ? color : UnknownTypeColor;
    public Color SetTypeColor(Type type, Color color) { _typeColors[type] = color; InvalidateVisual(); return color; }
    public Color SetTypeColor(Type type, Color color, bool replace) => replace || !_typeColors.ContainsKey(type) ? SetTypeColor(type, color) : _typeColors[type];
    public void MoveCanvas(float x, float y, bool animation, CanvasMoveArgs mode)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        if (mode.HasFlag(CanvasMoveArgs.All) || mode.HasFlag(CanvasMoveArgs.Left)) CanvasOffsetX = x;
        if (mode.HasFlag(CanvasMoveArgs.All) || mode.HasFlag(CanvasMoveArgs.Top)) CanvasOffsetY = y;
        CanvasMoved?.Invoke(this, EventArgs.Empty); UpdateTransform();
    }
    public void ScaleCanvas(float scale, float x, float y)
    {
        if (!float.IsFinite(scale) || !float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(scale));
        var before = ToWorld(new Point(x, y));
        CanvasScale = Math.Clamp(scale, .2f, 3f);
        CanvasOffsetX = x - (float)before.X * CanvasScale;
        CanvasOffsetY = y - (float)before.Y * CanvasScale;
        CanvasScaled?.Invoke(this, EventArgs.Empty); UpdateTransform();
    }
    public ConnectionInfo[] GetConnectionInfo() => _connections.ToArray();
    public static bool CanFindNodePath(XTNode start, XTNode find)
    {
        if (ReferenceEquals(start, find)) return true;
        var editor = start.Owner;
        if (editor is null || find.Owner != editor) return false;
        var pending = new Stack<XTNode>(); var visited = new HashSet<XTNode>(); pending.Push(start);
        while (pending.TryPop(out var node))
        {
            if (!visited.Add(node)) continue;
            foreach (var next in editor.Connections.Where(item => item.Output.Owner == node).Select(item => item.Input.Owner!))
            {
                if (ReferenceEquals(next, find)) return true;
                pending.Push(next);
            }
        }
        return false;
    }
    public bool AddXTNode(Type type) { if (!typeof(XTNode).IsAssignableFrom(type)) return false; Nodes.Add((XTNode)Activator.CreateInstance(type)!); return true; }
    public bool AddNode(XTNode node) { Nodes.Add(node); return true; }
    public void ShowAlert(string text, Color foreground, Color background) => ToolTip = text;
    public void ShowAlert(string text, Color foreground, Color background, AlertLocation location) => ShowAlert(text, foreground, background);
    public void ShowAlert(string text, Color foreground, Color background, int time, AlertLocation location, bool redraw) => ShowAlert(text, foreground, background);
    public string SetConnectionStatusText(ConnectionStatus status, string text) => text;
}
