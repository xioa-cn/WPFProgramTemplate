using Machine.ModuleLoad.Region;

using System.Windows;
using System.Globalization;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MachineApplication.Entrance.Models;

public class ObstructionRegionAnimation : IRegionAnimation
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(600);

    public bool IsEnabled { get; set; } = true;

    public Brush? Background { get; set; }

    public Brush? Foreground { get; set; }

    public Brush? TextForeground { get; set; }

    public string? Text { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    public void Animate(RegionAnimationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsEnabled || Duration <= TimeSpan.Zero || context.CurrentContent is null ||
            context.CancellationToken.IsCancellationRequested)
            return;

        var host = context.Host;
        var background = Background ?? Brushes.Transparent;
        var title = Window.GetWindow(host)?.Title;
        var text = Text ?? (string.IsNullOrWhiteSpace(title) ? "加载中…" : title);
        var obstruction = new ObstructionAdorner(host, background, Foreground, TextForeground, text);
        var animation = new DoubleAnimation(0, 1, new Duration(Duration))
        {
            EasingFunction = EasingFunction,
            FillBehavior = FillBehavior.Stop
        };
        AdornerLayer? layer = null;
        IDisposable? cleanupRegistration = null;
        var started = false;
        var completed = false;
        var contentOpacity = host.Opacity;
        var contentHidden = false;

        void HideContent()
        {
            if (contentHidden) return;
            contentOpacity = host.Opacity;
            host.Opacity = 0;
            contentHidden = true;
        }

        void ShowContent()
        {
            if (!contentHidden) return;
            host.Opacity = contentOpacity;
            contentHidden = false;
        }

        void Cleanup()
        {
            if (completed) return;
            completed = true;
            host.Loaded -= OnLoaded;
            host.SizeChanged -= OnSizeChanged;
            host.Unloaded -= OnUnloaded;
            host.PreviewKeyDown -= OnPreviewKeyDown;
            host.PreviewTextInput -= OnPreviewTextInput;
            animation.Completed -= OnCompleted;
            obstruction.BeginAnimation(ObstructionAdorner.ProgressProperty, null);
            obstruction.BeginAnimation(ObstructionAdorner.CycleProperty, null);
            layer?.Remove(obstruction);
            ShowContent();
            cleanupRegistration?.Dispose();
        }

        void Start()
        {
            if (completed || started) return;
            HideContent();
            if (layer is null)
            {
                layer = AdornerLayer.GetAdornerLayer(host);
                if (layer is null)
                {
                    if (host.IsLoaded) Cleanup();
                    return;
                }

                layer.Add(obstruction);
            }

            if (host.RenderSize.Width <= 0 || host.RenderSize.Height <= 0) return;
            started = true;
            obstruction.BeginAnimation(ObstructionAdorner.CycleProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(500)))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                }, HandoffBehavior.SnapshotAndReplace);
            obstruction.BeginAnimation(ObstructionAdorner.ProgressProperty, animation,
                HandoffBehavior.SnapshotAndReplace);
        }

        void OnLoaded(object sender, RoutedEventArgs args) => Start();
        void OnSizeChanged(object sender, SizeChangedEventArgs args) => Start();
        void OnUnloaded(object sender, RoutedEventArgs args) => Cleanup();
        void OnCompleted(object? sender, EventArgs args) => Cleanup();
        void OnPreviewKeyDown(object sender, KeyEventArgs args) => args.Handled = true;
        void OnPreviewTextInput(object sender, TextCompositionEventArgs args) => args.Handled = true;

        cleanupRegistration = context.RegisterCleanup(Cleanup);
        if (completed) return;

        host.Loaded += OnLoaded;
        host.SizeChanged += OnSizeChanged;
        host.Unloaded += OnUnloaded;
        host.PreviewKeyDown += OnPreviewKeyDown;
        host.PreviewTextInput += OnPreviewTextInput;
        animation.Completed += OnCompleted;
        Start();
    }

    private sealed class ObstructionAdorner : Adorner
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress), typeof(double), typeof(ObstructionAdorner),
            new PropertyMetadata(0d));

        public static readonly DependencyProperty CycleProperty = DependencyProperty.Register(
            nameof(Cycle), typeof(double), typeof(ObstructionAdorner),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly Brush _background;
        private readonly Brush? _foreground;
        private readonly Brush? _textForeground;
        private readonly string _text;
        private FormattedText? _formattedText;
        private double _pixelsPerDip;

        public ObstructionAdorner(UIElement adornedElement, Brush background, Brush? foreground,
            Brush? textForeground, string text)
            : base(adornedElement)
        {
            _background = background;
            _foreground = foreground;
            _textForeground = textForeground;
            _text = text;
            ClipToBounds = true;
        }

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public double Cycle
        {
            get => (double)GetValue(CycleProperty);
            set => SetValue(CycleProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = new Rect(RenderSize);
            drawingContext.DrawRectangle(_background, null, bounds);

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var foreground = _foreground
                ?? (AdornedElement as FrameworkElement)?.TryFindResource("MaterialDesign.Brush.Primary") as Brush
                ?? SystemColors.HighlightBrush;
            var textForeground = _textForeground ?? foreground;
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (_formattedText is null || _pixelsPerDip != pixelsPerDip)
            {
                _pixelsPerDip = pixelsPerDip;
                _formattedText = new FormattedText(_text, CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Medium,
                        FontStretches.Normal), 18, textForeground, pixelsPerDip)
                {
                    TextAlignment = TextAlignment.Center
                };
            }

            _formattedText.SetForegroundBrush(textForeground);
            _formattedText.MaxTextWidth = bounds.Width;
            var loaderTop = (bounds.Height - (48 + 50 + 18 + _formattedText.Height + 18)) / 2;
            var centerX = bounds.Width / 2;
            var cycle = Math.Clamp(Cycle, 0, 1);
            var bounce = 1 - Math.Abs(cycle * 2 - 1);
            var squash = Math.Max(0, 1 - Math.Abs(cycle * 4 - 2));
            var cornerRadius = cycle switch
            {
                <= 0.15 => 4 - cycle / 0.15,
                <= 0.5 => 3 + (cycle - 0.15) / 0.35 * 37,
                _ => 40 - (cycle - 0.5) / 0.5 * 36
            };

            drawingContext.DrawEllipse(foreground, null, new Point(centerX, loaderTop + 62.5),
                24 * (1 + 0.2 * bounce), 2.5);

            drawingContext.PushTransform(new TranslateTransform(centerX, loaderTop + 24 + 18 * bounce));
            drawingContext.PushTransform(new ScaleTransform(1, 1 - 0.1 * squash));
            drawingContext.PushTransform(new RotateTransform(90 * cycle));
            drawingContext.DrawGeometry(foreground, null, CreateBlock(cornerRadius));
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();

            drawingContext.DrawText(_formattedText, new Point(0, loaderTop + 48 + 50 + 18));
        }

        private static StreamGeometry CreateBlock(double bottomRightRadius)
        {
            var geometry = new StreamGeometry();
            var corner = new Size(4, 4);
            using (var path = geometry.Open())
            {
                path.BeginFigure(new Point(-20, -24), true, true);
                path.LineTo(new Point(20, -24), true, false);
                path.ArcTo(new Point(24, -20), corner, 0, false, SweepDirection.Clockwise, true, false);
                path.LineTo(new Point(24, 24 - bottomRightRadius), true, false);
                path.ArcTo(new Point(24 - bottomRightRadius, 24),
                    new Size(bottomRightRadius, bottomRightRadius), 0, false, SweepDirection.Clockwise, true, false);
                path.LineTo(new Point(-20, 24), true, false);
                path.ArcTo(new Point(-24, 20), corner, 0, false, SweepDirection.Clockwise, true, false);
                path.LineTo(new Point(-24, -20), true, false);
                path.ArcTo(new Point(-20, -24), corner, 0, false, SweepDirection.Clockwise, true, false);
            }

            geometry.Freeze();
            return geometry;
        }
    }
}
