using Machine.ModuleLoad.Region;

using System.Windows;
using System.Globalization;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MachineApplication.Entrance.Models;

public class ObstructionRegionAnimation : IRegionLoadingAnimation
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(600);

    public bool IsEnabled { get; set; } = true;

    public Brush? Background { get; set; }

    public Brush? Foreground { get; set; }

    public Brush? TextForeground { get; set; }

    public string? Text { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    public void Animate(RegionAnimationContext context)
        => Show(context, false);

    public void BeginLoading(RegionAnimationContext context)
        => Show(context, true);

    private void Show(RegionAnimationContext context, bool waitForContent)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsEnabled || (!waitForContent && (Duration <= TimeSpan.Zero || context.CurrentContent is null)) ||
            context.CancellationToken.IsCancellationRequested)
            return;

        var host = context.Host;
        var background = Background ?? Brushes.Transparent;
        var title = Window.GetWindow(host)?.Title;
        var text = Text ?? (string.IsNullOrWhiteSpace(title) ? "加载中…" : title);
        var obstruction = new ObstructionAdorner(host, background, Foreground, TextForeground, text);
        var animation = new DoubleAnimation(0, 1, new Duration(Duration > TimeSpan.Zero ? Duration : TimeSpan.Zero))
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
            obstruction.StopAnimation();
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
            obstruction.StartAnimation();
            if (!waitForContent)
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
            "Progress", typeof(double), typeof(ObstructionAdorner), new PropertyMetadata(0d));

        private static readonly DependencyProperty LoaderForegroundProperty = DependencyProperty.Register(
            "LoaderForeground", typeof(Brush), typeof(ObstructionAdorner),
            new FrameworkPropertyMetadata(SystemColors.HighlightBrush, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly DependencyProperty LoaderTextForegroundProperty = DependencyProperty.Register(
            "LoaderTextForeground", typeof(Brush), typeof(ObstructionAdorner),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly Brush _background;
        private readonly string _text;
        private readonly TranslateTransform _position = new();
        private readonly TranslateTransform _bounce = new();
        private readonly ScaleTransform _squash = new(1, 1);
        private readonly RotateTransform _rotation = new();
        private readonly TranslateTransform _shadowPosition = new();
        private readonly ScaleTransform _shadowScale = new(1, 1);
        private readonly LineSegment _cornerStart = new(new Point(24, 20), true);
        private readonly ArcSegment _corner = new(new Point(20, 24), new Size(4, 4), 0,
            false, SweepDirection.Clockwise, true);
        private readonly PathGeometry _block;
        private readonly List<(Animatable Target, DependencyProperty Property)> _animations = [];
        private FormattedText? _formattedText;
        private double _pixelsPerDip;

        public ObstructionAdorner(UIElement adornedElement, Brush background, Brush? foreground,
            Brush? textForeground, string text)
            : base(adornedElement)
        {
            _background = background;
            _text = text;
            ClipToBounds = true;
            if (foreground is null)
                SetResourceReference(LoaderForegroundProperty, "MaterialDesign.Brush.Primary");
            else
                SetValue(LoaderForegroundProperty, foreground);
            if (textForeground is not null)
                SetValue(LoaderTextForegroundProperty, textForeground);

            _block = new PathGeometry
            {
                Figures =
                {
                    new PathFigure(new Point(-20, -24),
                    [
                        new LineSegment(new Point(20, -24), true),
                        new ArcSegment(new Point(24, -20), new Size(4, 4), 0, false, SweepDirection.Clockwise, true),
                        _cornerStart,
                        _corner,
                        new LineSegment(new Point(-20, 24), true),
                        new ArcSegment(new Point(-24, 20), new Size(4, 4), 0, false, SweepDirection.Clockwise, true),
                        new LineSegment(new Point(-24, -20), true),
                        new ArcSegment(new Point(-20, -24), new Size(4, 4), 0, false, SweepDirection.Clockwise, true)
                    ], true)
                }
            };
        }

        public void StartAnimation()
        {
            if (_animations.Count != 0) return;
            Animate(_rotation, RotateTransform.AngleProperty, Frames((0, 0), (500, 90)));
            Animate(_bounce, TranslateTransform.YProperty, Frames((0, 0), (250, 18), (500, 0)));
            Animate(_squash, ScaleTransform.ScaleYProperty,
                Frames((0, 1), (125, 1), (250, 0.9), (375, 1), (500, 1)));
            Animate(_shadowScale, ScaleTransform.ScaleXProperty, Frames((0, 1), (250, 1.2), (500, 1)));

            var cornerStart = new PointAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(500), RepeatBehavior = RepeatBehavior.Forever
            };
            var cornerEnd = new PointAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(500), RepeatBehavior = RepeatBehavior.Forever
            };
            var cornerSize = new SizeAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(500), RepeatBehavior = RepeatBehavior.Forever
            };
            foreach (var (milliseconds, radius) in new (double, double)[] { (0, 4), (75, 3), (250, 40), (500, 4) })
            {
                var keyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds));
                cornerStart.KeyFrames.Add(new LinearPointKeyFrame(new Point(24, 24 - radius), keyTime));
                cornerEnd.KeyFrames.Add(new LinearPointKeyFrame(new Point(24 - radius, 24), keyTime));
                cornerSize.KeyFrames.Add(new LinearSizeKeyFrame(new Size(radius, radius), keyTime));
            }
            Animate(_cornerStart, LineSegment.PointProperty, cornerStart);
            Animate(_corner, ArcSegment.PointProperty, cornerEnd);
            Animate(_corner, ArcSegment.SizeProperty, cornerSize);
        }

        public void StopAnimation()
        {
            foreach (var (target, property) in _animations)
                target.BeginAnimation(property, null);
            _animations.Clear();
        }

        private void Animate(Animatable target, DependencyProperty property, AnimationTimeline animation)
        {
            _animations.Add((target, property));
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static DoubleAnimationUsingKeyFrames Frames(params (double Milliseconds, double Value)[] frames)
        {
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(500), RepeatBehavior = RepeatBehavior.Forever
            };
            foreach (var (milliseconds, value) in frames)
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(value,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds))));
            return animation;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = new Rect(RenderSize);
            drawingContext.DrawRectangle(_background, null, bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var foreground = (Brush)GetValue(LoaderForegroundProperty);
            var textForeground = (Brush?)GetValue(LoaderTextForegroundProperty) ?? foreground;
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
            _position.X = bounds.Width / 2;
            _position.Y = loaderTop + 24;
            _shadowPosition.X = bounds.Width / 2;
            _shadowPosition.Y = loaderTop + 62.5;

            drawingContext.PushTransform(_shadowPosition);
            drawingContext.PushTransform(_shadowScale);
            drawingContext.DrawEllipse(foreground, null, new Point(0, 0), 24, 2.5);
            drawingContext.Pop();
            drawingContext.Pop();

            drawingContext.PushTransform(_position);
            drawingContext.PushTransform(_bounce);
            drawingContext.PushTransform(_squash);
            drawingContext.PushTransform(_rotation);
            drawingContext.DrawGeometry(foreground, null, _block);
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();

            drawingContext.DrawText(_formattedText, new Point(0, loaderTop + 48 + 50 + 18));
        }
    }
}
