using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace CsxPad.Wpf.Controls;

internal sealed class BreakpointMargin : AbstractMargin, IBackgroundRenderer
{
    private const double MarginWidth = 20;
    private static readonly Brush BreakpointBrush = CreateBrush("#F05252");
    private static readonly Brush BreakpointBorderBrush = CreateBrush("#FF8A80");
    private static readonly Brush BreakpointHoverBrush = CreateBrush("#663B3F");
    private static readonly Brush PausedLineBrush = CreateBrush("#333726");
    private static readonly Brush PausedLineBorderBrush = CreateBrush("#D6B84B");
    private static readonly Brush ExecutionArrowBrush = CreateBrush("#FFD866");
    private static readonly Pen BreakpointPen = CreatePen(BreakpointBorderBrush, 1);
    private static readonly Pen PausedLinePen = CreatePen(PausedLineBorderBrush, 1);
    private readonly TextEditor _editor;
    private readonly Func<IReadOnlyCollection<int>> _getBreakpoints;
    private readonly Func<int?> _getCurrentLine;
    private readonly Action<int> _toggleBreakpoint;
    private int? _hoverLine;

    public BreakpointMargin(
        TextEditor editor,
        Func<IReadOnlyCollection<int>> getBreakpoints,
        Func<int?> getCurrentLine,
        Action<int> toggleBreakpoint)
    {
        _editor = editor;
        _getBreakpoints = getBreakpoints;
        _getCurrentLine = getCurrentLine;
        _toggleBreakpoint = toggleBreakpoint;
        Cursor = Cursors.Hand;
        ToolTip = "Toggle breakpoint (F9)";
    }

    public KnownLayer Layer => KnownLayer.Background;

    protected override Size MeasureOverride(Size availableSize) =>
        new(MarginWidth, 0);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        // A transparent drawing makes the empty gutter participate in WPF hit testing.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        TextView.EnsureVisualLines();
        var breakpoints = _getBreakpoints();

        foreach (var visualLine in TextView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine.LineNumber;
            var hasBreakpoint = breakpoints.Contains(line);
            if (!hasBreakpoint && _hoverLine != line)
            {
                continue;
            }

            var center = new Point(
                MarginWidth / 2,
                visualLine.VisualTop - TextView.ScrollOffset.Y + visualLine.Height / 2);
            if (_getCurrentLine() == line)
            {
                var arrow = new StreamGeometry();
                using (var context = arrow.Open())
                {
                    context.BeginFigure(new Point(4, center.Y - 6), true, true);
                    context.LineTo(new Point(15, center.Y), true, false);
                    context.LineTo(new Point(4, center.Y + 6), true, false);
                }

                arrow.Freeze();
                drawingContext.DrawGeometry(ExecutionArrowBrush, null, arrow);
                continue;
            }

            drawingContext.DrawEllipse(
                hasBreakpoint ? BreakpointBrush : BreakpointHoverBrush,
                hasBreakpoint ? BreakpointPen : null,
                center,
                5,
                5);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var line = GetLineFromViewportY(e.GetPosition(this).Y);
        if (!line.HasValue)
        {
            return;
        }

        _toggleBreakpoint(line.Value);
        e.Handled = true;
        Refresh();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var line = GetLineFromViewportY(e.GetPosition(this).Y);
        if (_hoverLine == line)
        {
            return;
        }

        _hoverLine = line;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverLine = null;
        InvalidateVisual();
    }

    public int? GetLineFromViewportY(double viewportY)
    {
        TextView.EnsureVisualLines();
        var visualLine = TextView.GetVisualLineFromVisualTop(viewportY + TextView.ScrollOffset.Y);
        return visualLine?.FirstDocumentLine.LineNumber;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var currentLine = _getCurrentLine();
        if (!currentLine.HasValue)
        {
            return;
        }

        foreach (var visualLine in textView.VisualLines)
        {
            if (currentLine.Value < visualLine.FirstDocumentLine.LineNumber ||
                currentLine.Value > visualLine.LastDocumentLine.LineNumber)
            {
                continue;
            }

            var y = visualLine.VisualTop - textView.ScrollOffset.Y;
            drawingContext.DrawRectangle(
                PausedLineBrush,
                PausedLinePen,
                new Rect(0, y, textView.ActualWidth, visualLine.Height));
        }
    }

    public void Refresh()
    {
        InvalidateVisual();
        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    public void RefreshViewport() => InvalidateVisual();

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
