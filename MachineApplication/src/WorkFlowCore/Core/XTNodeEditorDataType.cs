using System.Windows;
using System.Windows.Media;

namespace ST.Library.UI.NodeEditor;

public enum ConnectionStatus
{
    NoOwner, SameOwner, SameInputOrOutput, ErrorType, SingleOption, Loop, Exists,
    EmptyOption, Connected, DisConnected, Locked, Reject, Connecting, DisConnecting
}
public enum AlertLocation { Left, Top, Right, Bottom, Center, LeftTop, RightTop, RightBottom, LeftBottom }
[Flags]
public enum CanvasMoveArgs { Left = 1, Top = 2, All = 4 }

/// <summary>WPF 绘图上下文；自定义节点使用 DrawingContext，不再使用 GDI Graphics。</summary>
public readonly record struct DrawingTools(DrawingContext DrawingContext, double PixelsPerDip);
public struct NodeFindInfo
{
    public XTNode? Node;
    public XTNodeOption? NodeOption;
    public string? Mark;
    public string[]? MarkLines;
}
public struct ConnectionInfo
{
    public XTNodeOption Input;
    public XTNodeOption Output;
}
public delegate void XTNodeOptionEventHandler(object sender, XTNodeOptionEventArgs args);
public delegate void XTNodeEditorEventHandler(object sender, XTNodeEditorEventArgs args);
public delegate void XTNodeEditorOptionEventHandler(object sender, XTNodeEditorOptionEventArgs args);
public class XTNodeOptionEventArgs(bool isSponsor, XTNodeOption targetOption, ConnectionStatus status) : EventArgs
{
    public XTNodeOption TargetOption { get; } = targetOption;
    public ConnectionStatus Status { get; internal set; } = status;
    public bool IsSponsor { get; } = isSponsor;
}
public class XTNodeEditorEventArgs(XTNode node) : EventArgs
{
    public XTNode Node { get; } = node;
}
public class XTNodeEditorOptionEventArgs(XTNodeOption target, XTNodeOption current, ConnectionStatus status)
    : XTNodeOptionEventArgs(false, target, status)
{
    public XTNodeOption CurrentOption { get; } = current;
    public bool Continue { get; set; } = true;
}

/// <summary>主题绘制辅助；没有应用级主题时仍可单独使用编辑器。</summary>
internal static class NodeDrawing
{
    public static Brush Brush(FrameworkElement owner, string key, Color fallback) =>
        owner.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    public static void Text(DrawingTools tools, string? text, Rect bounds, Brush brush, double size = 12,
        bool right = false)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft YaHei UI"), size, brush, tools.PixelsPerDip)
        {
            MaxTextWidth = bounds.Width, MaxTextHeight = bounds.Height,
            Trimming = TextTrimming.CharacterEllipsis, TextAlignment = right ? TextAlignment.Right : TextAlignment.Left
        };
        tools.DrawingContext.DrawText(formatted, bounds.TopLeft);
    }
}
