using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace Machine.ModuleLoad.Utils;

public enum GrowlType
{
    Info,
    Success,
    Warning,
    Error,
    Fatal
}

public sealed class GrowlInfo
{
    public required string Message { get; init; }
    public string? Title { get; init; }
    public GrowlType Type { get; init; } = GrowlType.Info;
    public TimeSpan? Duration { get; init; }
    public string? Token { get; init; }
}

public sealed partial class Growl : ContentControl
{
    private static readonly List<WeakReference<Growl>> Hosts = [];
    private readonly StackPanel _items = new();
    private bool _isGlobal;
    private event EventHandler? ItemsChanged;

    public static readonly DependencyProperty TokenProperty = DependencyProperty.Register(
        nameof(Token), typeof(string), typeof(Growl), new PropertyMetadata(null));

    public static readonly DependencyProperty MaxCountProperty = DependencyProperty.Register(
        nameof(MaxCount), typeof(int), typeof(Growl),
        new PropertyMetadata(5, static (owner, _) => ((Growl)owner).Trim()),
        static value => (int)value > 0);

    public static readonly DependencyProperty DefaultDurationProperty = DependencyProperty.Register(
        nameof(DefaultDuration), typeof(TimeSpan), typeof(Growl),
        new PropertyMetadata(TimeSpan.FromSeconds(4)), static value => (TimeSpan)value >= TimeSpan.Zero);

    public static readonly DependencyProperty CloseTextProperty = DependencyProperty.Register(
        nameof(CloseText), typeof(string), typeof(Growl), new PropertyMetadata("Close"));

    public string? Token
    {
        get => (string?)GetValue(TokenProperty);
        set => SetValue(TokenProperty, value);
    }

    public int MaxCount
    {
        get => (int)GetValue(MaxCountProperty);
        set => SetValue(MaxCountProperty, value);
    }

    public TimeSpan DefaultDuration
    {
        get => (TimeSpan)GetValue(DefaultDurationProperty);
        set => SetValue(DefaultDurationProperty, value);
    }

    public string CloseText
    {
        get => (string)GetValue(CloseTextProperty);
        set => SetValue(CloseTextProperty, value);
    }

    public Growl()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        MaxWidth = 380;
        Focusable = false;
        Visibility = Visibility.Collapsed;
        Panel.SetZIndex(this, 6001);
        Content = new ScrollViewer
        {
            Content = _items,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public static void Info(string message, string? token = null) =>
        Show(new GrowlInfo { Message = message, Token = token });

    public static void Success(string message, string? token = null) =>
        Show(new GrowlInfo { Message = message, Type = GrowlType.Success, Token = token });

    public static void Warning(string message, string? token = null) =>
        Show(new GrowlInfo { Message = message, Type = GrowlType.Warning, Token = token });

    public static void Error(string message, string? token = null) =>
        Show(new GrowlInfo { Message = message, Type = GrowlType.Error, Token = token });

    public static void Fatal(string message, string? token = null) =>
        Show(new GrowlInfo { Message = message, Type = GrowlType.Fatal, Duration = TimeSpan.Zero, Token = token });

    public static void Show(GrowlInfo info)
    {
        Validate(info);
        OnApplicationDispatcher(() => FindHost(info.Token).Push(info));
    }

    public static void Clear(string? token = null) =>
        OnApplicationDispatcher(() => FindHost(token).ClearItems());

    public void Push(GrowlInfo info)
    {
        Validate(info);
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Push(info));
            return;
        }
        if (!IsLoaded) throw new InvalidOperationException("The Growl host must be loaded before showing notifications.");
        while (_items.Children.Count >= MaxCount) Remove((GrowlItem)_items.Children[0]);
        var item = new GrowlItem(this, info, info.Duration ?? DefaultDuration);
        _items.Children.Add(item);
        SetCurrentValue(VisibilityProperty, Visibility.Visible);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Validate(GrowlInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentException.ThrowIfNullOrWhiteSpace(info.Message);
        if (!Enum.IsDefined(info.Type)) throw new ArgumentOutOfRangeException(nameof(info.Type));
        if (info.Duration is { } duration && duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(info.Duration));
    }

    private static void OnApplicationDispatcher(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Growl requires a running WPF application.");
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private static Growl FindHost(string? token)
    {
        Hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
        var candidates = Hosts.Select(reference => reference.TryGetTarget(out var host) ? host : null)
            .OfType<Growl>()
            .Where(host => host.IsLoaded && string.Equals(host.Token, token, StringComparison.Ordinal))
            .ToArray();
        return candidates.LastOrDefault(host => Window.GetWindow(host) is { IsActive: true })
            ?? candidates.LastOrDefault(host => Window.GetWindow(host) is { IsVisible: true })
            ?? throw new InvalidOperationException("No visible Growl host is registered for the requested token.");
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isGlobal) return;
        Hosts.RemoveAll(reference => !reference.TryGetTarget(out var host) || ReferenceEquals(host, this));
        Hosts.Add(new WeakReference<Growl>(this));
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        Hosts.RemoveAll(reference => !reference.TryGetTarget(out var host) || ReferenceEquals(host, this));
        ClearItems();
    }

    private void Trim()
    {
        while (_items.Children.Count > MaxCount) Remove((GrowlItem)_items.Children[0]);
    }

    private void ClearItems()
    {
        foreach (var item in _items.Children.OfType<GrowlItem>().ToArray()) Remove(item);
    }

    private void Remove(GrowlItem item)
    {
        item.Stop();
        _items.Children.Remove(item);
        if (_items.Children.Count == 0) SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class GrowlItem : Border
    {
        private readonly Growl _owner;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _elapsed = new();
        private readonly TranslateTransform _translation = new();
        private readonly bool _persistent;
        private TimeSpan _remaining;
        private bool _closing;

        public GrowlItem(Growl owner, GrowlInfo info, TimeSpan duration)
        {
            _owner = owner;
            _remaining = duration;
            _persistent = duration == TimeSpan.Zero;
            _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
            _timer.Tick += OnTimer;
            Margin = new Thickness(10, 8, 10, 12);
            RenderTransform = _translation;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            var accentResource = info.Type switch
            {
                GrowlType.Success => "Growl.Brush.Success",
                GrowlType.Warning => "MaterialDesign.Brush.Secondary",
                GrowlType.Error or GrowlType.Fatal => "MaterialDesign.Brush.ValidationError",
                _ => "MaterialDesign.Brush.Primary"
            };
            var surface = new Grid();
            var shadow = new Border
            {
                CornerRadius = new CornerRadius(10),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 16,
                    ShadowDepth = 3,
                    Direction = 270,
                    Opacity = 0.14,
                    RenderingBias = RenderingBias.Performance
                }
            };
            shadow.SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
            surface.Children.Add(shadow);
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 10, 12),
                MinHeight = 56
            };
            card.SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
            surface.Children.Add(card);
            var outline = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Opacity = 0.45,
                IsHitTestVisible = false
            };
            outline.SetResourceReference(BorderBrushProperty, "MaterialDesign.Brush.Separator.Background");
            surface.Children.Add(outline);
            var accent = new Border
            {
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 14, 0, 14),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false
            };
            accent.SetResourceReference(BackgroundProperty, accentResource);
            surface.Children.Add(accent);
            AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(info.Title) ? info.Message : info.Title + ": " + info.Message);
            AutomationProperties.SetLiveSetting(this, info.Type is GrowlType.Error or GrowlType.Fatal
                ? AutomationLiveSetting.Assertive
                : AutomationLiveSetting.Polite);

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var hasTitle = !string.IsNullOrWhiteSpace(info.Title);
            var badge = new Grid
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = hasTitle ? VerticalAlignment.Top : VerticalAlignment.Center
            };
            var badgeBackground = new Border { CornerRadius = new CornerRadius(14), Opacity = 0.08 };
            badgeBackground.SetResourceReference(BackgroundProperty, accentResource);
            badge.Children.Add(badgeBackground);
            var icon = new PackIcon
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Kind = info.Type switch
                {
                    GrowlType.Success => PackIconKind.CheckCircleOutline,
                    GrowlType.Warning => PackIconKind.AlertOutline,
                    GrowlType.Error => PackIconKind.CloseCircleOutline,
                    GrowlType.Fatal => PackIconKind.AlertCircleOutline,
                    _ => PackIconKind.InformationOutline
                }
            };
            icon.SetResourceReference(Control.ForegroundProperty, accentResource);
            badge.Children.Add(icon);
            layout.Children.Add(badge);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            if (!string.IsNullOrWhiteSpace(info.Title))
            {
                var title = CreateText(info.Title);
                title.FontWeight = FontWeights.SemiBold;
                title.FontSize = 14;
                title.Margin = new Thickness(0, 0, 0, 4);
                text.Children.Add(title);
            }
            var message = CreateText(info.Message);
            if (hasTitle) message.Opacity = 0.78;
            text.Children.Add(message);
            layout.Children.Add(text);
            var close = new Button
            {
                Content = new PackIcon { Kind = PackIconKind.Close, Width = 14, Height = 14 },
                Width = 26,
                Height = 26,
                Padding = new Thickness(5),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = hasTitle ? VerticalAlignment.Top : VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var closeStyle = new Style(typeof(Button), owner.TryFindResource("MaterialDesignIconButton") as Style);
            closeStyle.Setters.Add(new Setter(OpacityProperty, 0.5));
            var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(OpacityProperty, 1d));
            closeStyle.Triggers.Add(hover);
            var focused = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true };
            focused.Setters.Add(new Setter(OpacityProperty, 1d));
            closeStyle.Triggers.Add(focused);
            close.Style = closeStyle;
            close.SetResourceReference(Control.ForegroundProperty, "MaterialDesign.Brush.Foreground");
            close.SetBinding(ToolTipProperty, new Binding(nameof(CloseText)) { Source = owner });
            close.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(CloseText)) { Source = owner });
            close.Click += (_, _) => Close();
            Grid.SetColumn(close, 2);
            layout.Children.Add(close);
            card.Child = layout;
            Child = surface;
            Loaded += OnItemLoaded;
            Unloaded += (_, _) => Stop();
            MouseEnter += (_, _) => Pause();
            MouseLeave += (_, _) => Resume();
            GotKeyboardFocus += (_, _) => Pause();
            LostKeyboardFocus += (_, _) => Resume();
        }

        private static TextBlock CreateText(string text)
        {
            var block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 20,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI")
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.Foreground");
            return block;
        }

        private void OnItemLoaded(object sender, RoutedEventArgs args)
        {
            Loaded -= OnItemLoaded;
            if (SystemParameters.ClientAreaAnimation)
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                _translation.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            Resume();
        }

        private void Pause()
        {
            _timer.Stop();
            if (!_elapsed.IsRunning) return;
            _remaining -= _elapsed.Elapsed;
            _elapsed.Reset();
        }

        private void Resume()
        {
            if (_persistent || _closing || IsMouseOver || IsKeyboardFocusWithin || !IsLoaded || _timer.IsEnabled) return;
            if (_remaining <= TimeSpan.Zero)
            {
                Close();
                return;
            }
            _timer.Interval = _remaining > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : _remaining;
            _elapsed.Restart();
            _timer.Start();
        }

        private void OnTimer(object? sender, EventArgs args)
        {
            Pause();
            Resume();
        }

        private void Close()
        {
            if (_closing) return;
            _closing = true;
            Pause();
            if (!SystemParameters.ClientAreaAnimation)
            {
                _owner.Remove(this);
                return;
            }
            var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140));
            animation.Completed += (_, _) => _owner.Remove(this);
            BeginAnimation(OpacityProperty, animation);
            _translation.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(_translation.X, 20, TimeSpan.FromMilliseconds(140)));
        }

        public void Stop()
        {
            _closing = true;
            _timer.Stop();
            _timer.Tick -= OnTimer;
            _elapsed.Reset();
            BeginAnimation(OpacityProperty, null);
            _translation.BeginAnimation(TranslateTransform.XProperty, null);
        }
    }
}
