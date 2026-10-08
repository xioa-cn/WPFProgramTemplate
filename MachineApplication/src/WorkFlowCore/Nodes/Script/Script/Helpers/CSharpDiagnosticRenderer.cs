using System.Windows;
using System.Windows.Media;
using CsxPad.Wpf.Services;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CsxPad.Wpf.Helpers;

internal sealed class CSharpDiagnosticRenderer : IBackgroundRenderer
{
    private static readonly Pen ErrorPen = CreatePen("#F14C4C");
    private static readonly Pen WarningPen = CreatePen("#CCA700");
    private IReadOnlyList<SemanticDiagnostic> _diagnostics = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Update(IReadOnlyList<SemanticDiagnostic> diagnostics, TextView textView)
    {
        _diagnostics = diagnostics;
        textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || textView.Document is null)
        {
            return;
        }

        foreach (var diagnostic in _diagnostics)
        {
            var start = Math.Clamp(diagnostic.Offset, 0, textView.Document.TextLength);
            var length = Math.Min(diagnostic.Length, textView.Document.TextLength - start);
            if (length <= 0)
            {
                continue;
            }

            var segment = new TextSegment { StartOffset = start, Length = length };
            foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment, false))
            {
                DrawWave(
                    drawingContext,
                    rectangle,
                    diagnostic.Severity == "error" ? ErrorPen : WarningPen);
            }
        }
    }

    private static void DrawWave(DrawingContext drawingContext, Rect rectangle, Pen pen)
    {
        var left = rectangle.Left;
        var right = Math.Max(rectangle.Right, left + 4);
        var baseline = rectangle.Bottom - 1;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(left, baseline), false, false);
            var raised = true;
            for (var x = left + 2; x < right; x += 2)
            {
                context.LineTo(new Point(x, baseline + (raised ? -1.5 : 1.5)), true, false);
                raised = !raised;
            }

            context.LineTo(new Point(right, baseline), true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static Pen CreatePen(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        var pen = new Pen(brush, 1.2);
        pen.Freeze();
        return pen;
    }
}
