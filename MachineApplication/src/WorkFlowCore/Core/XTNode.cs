using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace ST.Library.UI.NodeEditor;

/// <summary>原生 WPF 节点。位置使用画布 DIP，绘制扩展通过 DrawingContext 完成。</summary>
public abstract class XTNode : Canvas, INotifyPropertyChanged
{
    private static readonly Geometry NodeIconGeometry = CreateIconGeometry(PackIconKind.CubeOutline);
    private static readonly Geometry StartIconGeometry = CreateIconGeometry(PackIconKind.PlayCircleOutline);

    private static Geometry CreateIconGeometry(PackIconKind kind)
    {
        var geometry = Geometry.Parse(new PackIcon { Kind = kind }.Data);
        geometry.Freeze();
        return geometry;
    }

    private XTNodeEditor? _owner;
    private string _title = "Node", _mark = "", _runtimeText = "";
    private bool _selected, _active;
    private Color _titleColor = Colors.Transparent, _foreColor = Colors.Transparent, _backColor = Colors.Transparent;
    private Color _runtimeHighlightColor = Colors.Transparent, _runtimeTextColor = Colors.Transparent;

    public XTNodeEditor? Owner
    {
        get => _owner;
        internal set
        {
            _owner = value;
            OnOwnerChanged();
            BuildSize(true, true, true);
        }
    }

    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Changed();
            OnSelectedChanged();
            Owner?.NotifySelectionChanged();
        }
    }

    public bool IsActive
    {
        get => _active;
        internal set
        {
            if (_active == value) return;
            _active = value;
            Changed();
            OnActiveChanged();
        }
    }

    public string Title
    {
        get => _title;
        protected set
        {
            _title = value;
            Changed();
            BuildSize(true, true, true);
        }
    }

    public string Mark
    {
        get => _mark;
        set
        {
            _mark = value ?? "";
            Changed();
        }
    }

    public string[] MarkLines => Mark.Split('\n');

    public string RuntimeText
    {
        get => _runtimeText;
        set
        {
            _runtimeText = value ?? "";
            Changed();
        }
    }

    public Color RuntimeTextColor
    {
        get => _runtimeTextColor;
        set
        {
            _runtimeTextColor = value;
            Changed();
        }
    }

    public Color RuntimeHighlightColor
    {
        get => _runtimeHighlightColor;
        set
        {
            _runtimeHighlightColor = value;
            Changed();
        }
    }

    public Color TitleColor
    {
        get => _titleColor;
        protected set
        {
            _titleColor = value;
            Changed();
        }
    }

    public Color ForeColor
    {
        get => _foreColor;
        protected set
        {
            _foreColor = value;
            Changed();
        }
    }

    public Color BackColor
    {
        get => _backColor;
        protected set
        {
            _backColor = value;
            Changed();
        }
    }

    public Color MarkColor { get; protected set; } = Colors.Transparent;

    public double Left
    {
        get => ReadPosition(GetLeft(this));
        set
        {
            VerifyNumber(value);
            SetLeft(this, value);
            Moved();
        }
    }

    public double Top
    {
        get => ReadPosition(GetTop(this));
        set
        {
            VerifyNumber(value);
            SetTop(this, value);
            Moved();
        }
    }

    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public Point Location
    {
        get => new(Left, Top);
        set
        {
            Left = value.X;
            Top = value.Y;
        }
    }

    public Size Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
            BuildSize(false, true, true);
        }
    }

    public Rect Rectangle => new(Location, Size);
    public Rect TitleRectangle => new(Left, Top, Width, TitleHeight);
    public Rect MarkRectangle => new(Left, Top - 24, Width, 22);
    public double TitleHeight { get; protected set; } = 34;
    public double ItemHeight { get; protected set; } = 26;

    /// <summary>为业务节点的类型摘要预留空间，基础节点和自定义控件默认不增加偏移。</summary>
    protected virtual double BodyHeaderHeight => 0;

    public bool AutoSize { get; protected set; } = true;
    public bool LockOption { get; set; }
    public bool LockLocation { get; set; }
    public Guid Guid { get; internal set; } = Guid.NewGuid();
    public bool LetGetOptions { get; protected set; } = true;
    protected internal XTNodeOptionCollection InputOptions { get; }
    protected internal XTNodeOptionCollection OutputOptions { get; }
    protected XTNodeControlCollection Controls { get; }
    public int InputOptionsCount => InputOptions.Count;
    public int OutputOptionsCount => OutputOptions.Count;
    public int ControlsCount => Controls.Count;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>保留 OnCreate 初始化入口；派生类应在该入口建立端口和自定义控件。</summary>
    protected XTNode()
    {
        Width = 180;
        Height = 90;
        Focusable = true;
        InputOptions = new(this, true);
        OutputOptions = new(this, false);
        Controls = new(this);
        // 背景由绘制方法统一处理，透明命中区域避免矩形底色覆盖圆角和辉光。
        Background = Brushes.Transparent;
        SizeChanged += (_, _) =>
        {
            SetOptionsLocation();
            Owner?.InvalidateVisual();
        };
        OnCreate();
        BuildSize(true, true, false);
    }

    protected virtual void OnCreate()
    {
    }

    protected virtual void OnOwnerChanged()
    {
    }

    protected virtual void OnSelectedChanged()
    {
    }

    protected virtual void OnActiveChanged()
    {
    }

    protected internal virtual bool IsPropertyVisible(string propertyName) => true;

    protected internal virtual void OnEditorLoadCompleted()
    {
    }

    protected virtual void OnMove(EventArgs args)
    {
    }

    protected virtual void OnResize(EventArgs args)
    {
    }

    private static double ReadPosition(double value) => double.IsNaN(value) ? 0 : value;

    private static void VerifyNumber(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void Moved()
    {
        SetOptionsLocation();
        OnMove(EventArgs.Empty);
        Changed(nameof(Location));
    }

    private void Changed([CallerMemberName] string? property = null)
    {
        Invalidate();
        PropertyChanged?.Invoke(this, new(property));
    }

    public void Invalidate()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(Invalidate);
            return;
        }

        InvalidateVisual();
        Owner?.InvalidateVisual();
    }

    public void Invalidate(Rect rectangle) => Invalidate();
    public XTNodeOption[] GetInputOptions() => LetGetOptions ? InputOptions.ToArray() : [];
    public XTNodeOption[] GetOutputOptions() => LetGetOptions ? OutputOptions.ToArray() : [];

    /// <summary>仅根据当前端口计算布局，不要求控件已经加入窗口。</summary>
    protected internal void BuildSize(bool buildNode, bool buildMark, bool redraw)
    {
        if (InputOptions is null || OutputOptions is null) return;
        if (buildNode && AutoSize)
        {
            Width = Math.Max(220, Math.Max(Title.Length * 9 + 60,
                InputOptions.Select(option => option.Text.Length).DefaultIfEmpty().Max() * 8 +
                OutputOptions.Select(option => option.Text.Length).DefaultIfEmpty().Max() * 8 + 56));
            Height = TitleHeight + BodyHeaderHeight +
                     Math.Max(1, Math.Max(InputOptions.Count, OutputOptions.Count)) * ItemHeight + 12;
            // 上下排列时按真实端口数量分配水平槽位，不让多个端口和标签挤在同一点。
            if (Owner?.VerticalPorts == true)
            {
                var columns = Math.Max(1, Math.Max(InputOptions.Count(option => option != XTNodeOption.Empty),
                    OutputOptions.Count(option => option != XTNodeOption.Empty)));
                Width = Math.Max(Width, columns * 88 + 24);
            }
            OnResize(EventArgs.Empty);
        }

        SetOptionsLocation();
        if (Controls is not null)
            foreach (var control in Controls)
                control.UpdateLocation();
        if (redraw) Invalidate();
    }

    /// <summary>同时更新端口圆点、命中矩形和标签，确保切换方向后拖线位置与显示一致。</summary>
    protected virtual void SetOptionsLocation()
    {
        Layout(InputOptions, true);
        Layout(OutputOptions, false);

        void Layout(XTNodeOptionCollection options, bool input)
        {
            var visibleOptions = options.Where(option => option != XTNodeOption.Empty).ToArray();
            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (option == XTNodeOption.Empty) continue;
                if (Owner?.VerticalPorts == true)
                {
                    var column = Array.IndexOf(visibleOptions, option);
                    var slotWidth = (Width - 24) / visibleOptions.Length;
                    var centerX = Left + 12 + slotWidth * (column + .5);
                    var verticalPoint = OnSetOptionDotLocation(option,
                        new Point(centerX - option.DotSize / 2, (input ? Top : Bottom) - option.DotSize / 2), index);
                    option.DotLeft = verticalPoint.X;
                    option.DotTop = verticalPoint.Y;
                    // 输入标签放在节点上沿之外，不覆盖标题；输出标签靠近底部端口。
                    option.TextRectangle = OnSetOptionTextRectangle(option,
                        new Rect(Left + 12 + slotWidth * column, input ? Top - 25 : Bottom - 25,
                            slotWidth, 18), index);
                    continue;
                }
                var point = OnSetOptionDotLocation(option,
                    new Point(input ? Left - option.DotSize / 2 : Right - option.DotSize / 2,
                        Top + TitleHeight + BodyHeaderHeight + ItemHeight * index + (ItemHeight - option.DotSize) / 2),
                    index);
                option.DotLeft = point.X;
                option.DotTop = point.Y;
                option.TextRectangle = OnSetOptionTextRectangle(option,
                    new Rect(input ? Left + 14 : Left + Width / 2,
                        Top + TitleHeight + BodyHeaderHeight + index * ItemHeight + 5,
                        Width / 2 - 12, ItemHeight - 4), index);
            }
        }
    }

    protected virtual Point OnSetOptionDotLocation(XTNodeOption option, Point point, int index) => point;
    protected virtual Rect OnSetOptionTextRectangle(XTNodeOption option, Rect rectangle, int index) => rectangle;

    protected override void OnRender(DrawingContext context)
    {
        OnDrawNode(new(context, VisualTreeHelper.GetDpi(this).PixelsPerDip));
    }

    /// <summary>绘制深色渐变节点与多层细描边，高亮只改变视觉效果，不改变端口命中区域。</summary>
    protected internal virtual void OnDrawNode(DrawingTools tools)
    {
        var context = tools.DrawingContext;
        var bounds = new Rect(0, 0, Width, Height);
        var accent = GetAccentColor();
        var highlighted = IsSelected || IsActive;
        // 以少量透明描边代替整张画布的模糊特效，降低拖动大量节点时的绘制开销。
        if (highlighted)
        {
            context.DrawRoundedRectangle(null,
                new Pen(new SolidColorBrush(Color.FromArgb(18, accent.R, accent.G, accent.B)), 12), bounds, 10, 10);
            context.DrawRoundedRectangle(null,
                new Pen(new SolidColorBrush(Color.FromArgb(42, accent.R, accent.G, accent.B)), 5), bounds, 9, 9);
        }

        var surfaceTop =
            (NodeDrawing.Brush(this, "Workflow.Node.Surface", Color.FromRgb(43, 52, 72)) as SolidColorBrush)?.Color ??
            Color.FromRgb(43, 52, 72);
        var surfaceBottom =
            (NodeDrawing.Brush(this, "Workflow.Node.SurfaceBottom", Color.FromRgb(33, 42, 60)) as SolidColorBrush)
            ?.Color ?? Color.FromRgb(33, 42, 60);
        Brush surface = BackColor.A == 0
            ? new LinearGradientBrush(surfaceTop, surfaceBottom, 90)
            : new SolidColorBrush(BackColor);
        var outline = highlighted
            ? new SolidColorBrush(accent)
            : NodeDrawing.Brush(this, "Workflow.Node.Border", Color.FromRgb(87, 103, 128));
        context.DrawRoundedRectangle(surface, new Pen(outline, highlighted ? 1.5 : 1), bounds, 9, 9);
        context.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(30, 226, 235, 255)), 1),
            new Rect(3, 3, Width - 6, Height - 6), 6, 6);
        OnDrawTitle(tools);
        OnDrawBody(tools);
        OnDrawMark(tools);
        if (RuntimeHighlightColor.A > 0)
            context.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(RuntimeHighlightColor), 3),
                new Rect(0, 0, Width, Height), 8, 8);
        if (RuntimeText.Length > 0)
            NodeDrawing.Text(tools, RuntimeText, new Rect(0, Height + 4, Math.Max(Width, 200), 24),
                RuntimeTextColor.A > 0 ? new SolidColorBrush(RuntimeTextColor) : ForegroundBrush);
    }

    private Brush ForegroundBrush => ForeColor.A > 0
        ? new SolidColorBrush(ForeColor)
        : NodeDrawing.Brush(this, "MaterialDesign.Brush.Foreground", Colors.Black);

    /// <summary>节点分类色优先于页面主色，用于标题、端口和连线的统一识别。</summary>
    internal Color GetAccentColor() => TitleColor.A > 0
        ? Color.FromRgb(TitleColor.R, TitleColor.G, TitleColor.B)
        : Color.FromRgb(142, 119, 227);

    /// <summary>渐变标题保留顶部圆角；标题文字固定浅色，避免宿主浅色主题降低对比度。</summary>
    protected virtual void OnDrawTitle(DrawingTools tools)
    {
        var context = tools.DrawingContext;
        var accent = GetAccentColor();
        var gradient = new LinearGradientBrush(Color.FromArgb(190, accent.R, accent.G, accent.B),
            Color.FromArgb(80, accent.R, accent.G, accent.B), 90);
        context.PushClip(new RectangleGeometry(new Rect(2, 2, Width - 4, Height - 4), 7, 7));
        context.DrawRectangle(gradient, null, new Rect(2, 2, Width - 4, TitleHeight - 2));
        context.Pop();
        var isStartNode = this is IEditorStartNode;
        var iconBrush = NodeDrawing.Brush(this, isStartNode ? "Workflow.Green" : "Workflow.Accent",
            isStartNode ? Color.FromRgb(99, 215, 171) : Color.FromRgb(172, 140, 255));
        context.PushTransform(new TranslateTransform(10, (TitleHeight - 20) / 2));
        context.PushTransform(new ScaleTransform(20.0 / 24, 20.0 / 24));
        context.DrawGeometry(iconBrush, null, isStartNode ? StartIconGeometry : NodeIconGeometry);
        context.Pop();
        context.Pop();
        NodeDrawing.Text(tools, Title, new Rect(38, 8, Width - 52, TitleHeight - 10), Brushes.White, 13);
    }

    protected virtual void OnDrawBody(DrawingTools tools)
    {
        foreach (var option in InputOptions.Concat(OutputOptions))
        {
            if (option == XTNodeOption.Empty) continue;
            OnDrawOptionDot(tools, option);
            OnDrawOptionText(tools, option);
        }
    }

    protected internal virtual void OnDrawMark(DrawingTools tools)
    {
        if (Mark.Length > 0) NodeDrawing.Text(tools, Mark, new Rect(0, -24, Width, 22), ForegroundBrush);
    }

    /// <summary>端口使用节点分类色与浅色内芯，连接状态可直接从亮度区分。</summary>
    protected virtual void OnDrawOptionDot(DrawingTools tools, XTNodeOption option)
    {
        var color = option.DotColor.A > 0 ? option.DotColor : GetAccentColor();
        var center = new Point(option.Center.X - Left, option.Center.Y - Top);
        if (option.IsUsingDefaultValue)
        {
            var radius = option.DotSize / 2;
            var diamond = new StreamGeometry();
            using (var geometry = diamond.Open())
            {
                geometry.BeginFigure(new Point(center.X, center.Y - radius), true, true);
                geometry.LineTo(new Point(center.X + radius, center.Y), true, false);
                geometry.LineTo(new Point(center.X, center.Y + radius), true, false);
                geometry.LineTo(new Point(center.X - radius, center.Y), true, false);
            }
            diamond.Freeze();
            tools.DrawingContext.DrawGeometry(new SolidColorBrush(color), new Pen(Brushes.White, 1), diamond);
            return;
        }
        tools.DrawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)), null,
            center, option.DotSize / 2 + 3, option.DotSize / 2 + 3);
        tools.DrawingContext.DrawEllipse(
            option.ConnectionCount > 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(29, 39, 55)),
            new Pen(new SolidColorBrush(color), 1.5), center, option.DotSize / 2 - 1, option.DotSize / 2 - 1);
    }

    protected virtual void OnDrawOptionText(DrawingTools tools, XTNodeOption option)
    {
        var bounds = option.TextRectangle;
        bounds.Offset(-Left, -Top);
        if (Owner?.VerticalPorts == true)
        {
            var text = new FormattedText(option.Text, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft YaHei UI"), 11,
                option.TextColor.A > 0 ? new SolidColorBrush(option.TextColor) : ForegroundBrush, tools.PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, bounds.Width), MaxTextHeight = bounds.Height,
                Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center
            };
            tools.DrawingContext.DrawText(text, bounds.TopLeft);
            return;
        }
        NodeDrawing.Text(tools, option.Text, bounds,
            option.TextColor.A > 0 ? new SolidColorBrush(option.TextColor) : ForegroundBrush,
            right: !option.IsInput);
    }

    protected bool SetOptionText(XTNodeOption option, string text)
    {
        if (option.Owner != this) return false;
        option.Text = text;
        BuildSize(true, true, true);
        return true;
    }

    protected bool SetOptionTextColor(XTNodeOption option, Color color)
    {
        if (option.Owner != this) return false;
        option.TextColor = color;
        Invalidate();
        return true;
    }

    protected bool SetOptionDotColor(XTNodeOption option, Color color)
    {
        if (option.Owner != this) return false;
        option.DotColor = color;
        Invalidate();
        return true;
    }

    /// <summary>持久化自定义属性仍使用原描述器字节接口，方便派生节点复用业务存储逻辑。</summary>
    internal Dictionary<string, byte[]> SaveState()
    {
        var values = new Dictionary<string, byte[]>();
        foreach (var descriptor in XTNodePropertyDescriptor.Create(this))
            values[descriptor.PropertyInfo.Name] = descriptor.GetBytesFromValue();
        OnSaveNode(values);
        return values;
    }

    protected virtual void OnSaveNode(Dictionary<string, byte[]> values)
    {
    }

    protected internal virtual void OnLoadNode(Dictionary<string, byte[]> values)
    {
        foreach (var descriptor in XTNodePropertyDescriptor.Create(this))
            if (values.TryGetValue(descriptor.PropertyInfo.Name, out var bytes))
                descriptor.SetValue(bytes);
    }
}
