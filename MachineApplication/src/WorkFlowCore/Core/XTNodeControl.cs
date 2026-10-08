using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ST.Library.UI.NodeEditor;

/// <summary>节点内部原生 WPF 内容控件，可在 Content 中放置 TextBox、Button 等控件。</summary>
public class XTNodeControl : ContentControl
{
    public XTNode? Owner { get; internal set; }

    public double Left
    {
        get => double.IsNaN(Canvas.GetLeft(this)) ? 0 : Canvas.GetLeft(this);
        set => Canvas.SetLeft(this, value);
    }

    private double _top;

    public double Top
    {
        get => _top;
        set
        {
            _top = value;
            UpdateLocation();
        }
    }

    public double Right => Left + (double.IsNaN(Width) ? ActualWidth : Width);
    public double Bottom => Top + (double.IsNaN(Height) ? ActualHeight : Height);

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
        get => new(Math.Max(0, Right - Left), Math.Max(0, Bottom - Top));
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public Rect DisplayRectangle => new(Location, Size);
    public Rect ClientRectangle => new(new Point(), Size);

    public string Text
    {
        get => Content as string ?? "";
        set => Content = value;
    }

    public Color BackColor
    {
        get => (Background as SolidColorBrush)?.Color ?? Colors.Transparent;
        set => Background = new SolidColorBrush(value);
    }

    public Color ForeColor
    {
        get => (Foreground as SolidColorBrush)?.Color ?? Colors.Transparent;
        set => Foreground = new SolidColorBrush(value);
    }

    public bool Enabled
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    public bool Visable
    {
        get => Visibility == Visibility.Visible;
        set => Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Invalidate() => InvalidateVisual();

    public Rect RectangleToParent(Rect rect)
    {
        rect.Offset(Left, Top);
        return rect;
    }

    internal void UpdateLocation() => Canvas.SetTop(this, Top + (Owner?.TitleHeight ?? 0));
    public event XTNodeControlPaintEventHandler? Paint;

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        Paint?.Invoke(this, new(new(context, VisualTreeHelper.GetDpi(this).PixelsPerDip)));
    }
}

public delegate void XTNodeControlPaintEventHandler(object sender, XTNodeControlPaintEventArgs args);

public sealed class XTNodeControlPaintEventArgs(DrawingTools drawingTools) : EventArgs
{
    public DrawingTools DrawingTools { get; } = drawingTools;
}