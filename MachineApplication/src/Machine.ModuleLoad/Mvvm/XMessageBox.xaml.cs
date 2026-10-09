using System.ComponentModel;
using System.Globalization;
using System.Media;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace Machine.ModuleLoad.Mvvm;

public partial class XMessageBox : Window
{
    private MessageBoxResult _result;
    private MessageBoxResult _closeResult;
    private Button? _defaultButton;
    private string _message = "";
    private readonly List<string> _buttonLabels = [];

    public XMessageBox()
    {
        InitializeComponent();
        ContentRendered += OnContentRendered;
        Configure("", "", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);
    }

    public static MessageBoxResult Show(string messageBoxText) => Show(messageBoxText, "");

    public static MessageBoxResult Show(string messageBoxText, string caption) => Show(messageBoxText, caption, MessageBoxButton.OK);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) =>
        Show(messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) =>
        Show(messageBoxText, caption, button, icon, MessageBoxResult.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon,
        MessageBoxResult defaultResult) => Show(messageBoxText, caption, button, icon, defaultResult, MessageBoxOptions.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button,
        MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options) =>
        ShowCore(null, messageBoxText, caption, button, icon, defaultResult, options);

    public static MessageBoxResult Show(Window owner, string messageBoxText) => Show(owner, messageBoxText, "");

    public static MessageBoxResult Show(Window owner, string messageBoxText, string caption) =>
        Show(owner, messageBoxText, caption, MessageBoxButton.OK);

    public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button) =>
        Show(owner, messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button,
        MessageBoxImage icon) => Show(owner, messageBoxText, caption, button, icon, MessageBoxResult.None);

    public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button,
        MessageBoxImage icon, MessageBoxResult defaultResult) =>
        Show(owner, messageBoxText, caption, button, icon, defaultResult, MessageBoxOptions.None);

    public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button,
        MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options) =>
        ShowCore(owner, messageBoxText, caption, button, icon, defaultResult, options);

    private static MessageBoxResult ShowCore(Window? owner, string message, string caption, MessageBoxButton buttons,
        MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
    {
        Validate(buttons, icon, defaultResult, options);
        if ((options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) != 0)
        {
            if (owner is not null) throw new ArgumentException("服务通知或默认桌面模式不能指定 owner。", nameof(owner));
            return MessageBox.Show(message, caption, buttons, icon, defaultResult, options);
        }

        var dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                throw new InvalidOperationException("无法在已关闭的 UI 调度器上显示消息框。");
            if (!dispatcher.CheckAccess())
                return dispatcher.Invoke(() => ShowCore(owner, message, caption, buttons, icon, defaultResult, options));
        }
        else if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            MessageBoxResult result = MessageBoxResult.None;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { result = ShowCore(null, message, caption, buttons, icon, defaultResult, options); }
                catch (Exception error) { failure = error; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var dialog = new XMessageBox();
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Topmost = owner.Topmost;
        }
        dialog.ApplyContrastingTheme(owner);
        dialog.Configure(message, caption, buttons, icon, defaultResult, options);
        PlaySound(icon);
        dialog.ShowDialog();
        return dialog._result;
    }

    private void ApplyContrastingTheme(Window? owner)
    {
        Color GetHostColor(string key, Color fallback) =>
            (owner?.TryFindResource(key) as SolidColorBrush ??
             Application.Current?.TryFindResource(key) as SolidColorBrush)?.Color ?? fallback;

        var background = GetHostColor("MaterialDesign.Brush.Card.Background", Colors.White);
        var brightness = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255;
        var theme = Theme.Create(brightness < 0.5 ? BaseTheme.Light : BaseTheme.Dark,
            GetHostColor("MaterialDesign.Brush.Primary", Colors.RoyalBlue),
            GetHostColor("MaterialDesign.Brush.Secondary", Colors.Orange));
        var resources = new ResourceDictionary
        {
            Source = new Uri("/Machine.ModuleLoad;component/Mvvm/XMessageBoxTheme.xaml", UriKind.Relative)
        };
        resources.SetTheme(theme);
        Resources.MergedDictionaries.Add(resources);
    }

    private static void Validate(MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult result, MessageBoxOptions options)
    {
        if (!Enum.IsDefined(buttons)) throw new InvalidEnumArgumentException(nameof(buttons), (int)buttons, typeof(MessageBoxButton));
        if (!Enum.IsDefined(icon)) throw new InvalidEnumArgumentException(nameof(icon), (int)icon, typeof(MessageBoxImage));
        if (!Enum.IsDefined(result)) throw new InvalidEnumArgumentException(nameof(result), (int)result, typeof(MessageBoxResult));
        const MessageBoxOptions allowed = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading |
            MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly;
        if ((options & ~allowed) != 0) throw new InvalidEnumArgumentException(nameof(options), (int)options, typeof(MessageBoxOptions));
    }

    private void Configure(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon,
        MessageBoxResult defaultResult, MessageBoxOptions options)
    {
        _message = message ?? "";
        Title = caption ?? "";
        MessageText.Text = _message;
        AutomationProperties.SetName(this, Title);
        MessageText.FlowDirection = options.HasFlag(MessageBoxOptions.RtlReading) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        MessageText.TextAlignment = options.HasFlag(MessageBoxOptions.RightAlign) ? TextAlignment.Right : TextAlignment.Left;
        _closeResult = buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.YesNo => MessageBoxResult.None,
            _ => MessageBoxResult.Cancel
        };
        CloseButton.IsEnabled = _closeResult != MessageBoxResult.None;
        var chinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        var closeLabel = chinese ? "关闭" : "Close";
        CloseButton.ToolTip = closeLabel;
        AutomationProperties.SetName(CloseButton, closeLabel);
        MessageIcon.Visibility = icon == MessageBoxImage.None ? Visibility.Collapsed : Visibility.Visible;
        MessageIcon.Kind = icon switch
        {
            MessageBoxImage.Error => PackIconKind.CloseCircle,
            MessageBoxImage.Warning => PackIconKind.Alert,
            MessageBoxImage.Question => PackIconKind.HelpCircle,
            _ => PackIconKind.Information
        };
        MessageIcon.SetResourceReference(ForegroundProperty, icon == MessageBoxImage.Error
            ? "MaterialDesign.Brush.ValidationError" : "MaterialDesign.Brush.Primary");
        MessageBoxResult[] results = buttons switch
        {
            MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel],
            MessageBoxButton.YesNo => [MessageBoxResult.Yes, MessageBoxResult.No],
            MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],
            _ => [MessageBoxResult.OK]
        };
        if (!results.Contains(defaultResult)) defaultResult = results[0];
        ButtonPanel.Children.Clear();
        _buttonLabels.Clear();
        foreach (var result in results)
        {
            var label = GetLabel(result, chinese);
            _buttonLabels.Add(label.Replace("_", ""));
            var action = new Button
            {
                Content = label,
                MinWidth = 88,
                Height = 36,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(16, 0, 16, 0),
                IsDefault = result == defaultResult
            };
            action.SetResourceReference(StyleProperty, result == defaultResult ? "MaterialDesignRaisedButton" : "MaterialDesignOutlinedButton");
            action.Click += (_, _) => Complete(result);
            ButtonPanel.Children.Add(action);
            if (result == defaultResult) _defaultButton = action;
        }
        var area = SystemParameters.WorkArea;
        Width = Math.Min(480, Math.Max(200, area.Width - 32));
        MaxHeight = Math.Max(180, area.Height - 32);
        MessageScroller.MaxHeight = Math.Max(40, MaxHeight - 170);
    }

    private static string GetLabel(MessageBoxResult result, bool chinese) => (result, chinese) switch
    {
        (MessageBoxResult.OK, true) => "确定(_O)",
        (MessageBoxResult.Cancel, true) => "取消(_C)",
        (MessageBoxResult.Yes, true) => "是(_Y)",
        (MessageBoxResult.No, true) => "否(_N)",
        (MessageBoxResult.OK, false) => "_OK",
        (MessageBoxResult.Cancel, false) => "_Cancel",
        (MessageBoxResult.Yes, false) => "_Yes",
        _ => "_No"
    };

    private void OnContentRendered(object? sender, EventArgs args) => _defaultButton?.Focus();

    private void Complete(MessageBoxResult result)
    {
        _result = result;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs args)
    {
        if (_closeResult != MessageBoxResult.None) Complete(_closeResult);
    }

    private void OnTitleMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (args.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        if (_result == MessageBoxResult.None)
        {
            if (_closeResult == MessageBoxResult.None) args.Cancel = true;
            else _result = _closeResult;
        }
        base.OnClosing(args);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            if (_closeResult != MessageBoxResult.None) Complete(_closeResult);
            args.Handled = true;
        }
        else if (args.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            try
            {
                Clipboard.SetText($"---------------------------\r\n{Title}\r\n---------------------------\r\n{_message}\r\n---------------------------\r\n{string.Join("   ", _buttonLabels)}\r\n---------------------------");
            }
            catch (ExternalException) { }
            args.Handled = true;
        }
        else if (args.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is Button button)
        {
            var index = ButtonPanel.Children.IndexOf(button);
            if (index >= 0)
            {
                var count = ButtonPanel.Children.Count;
                ((Button)ButtonPanel.Children[(index + (args.Key == Key.Right ? 1 : count - 1)) % count]).Focus();
                args.Handled = true;
            }
        }
        base.OnPreviewKeyDown(args);
    }

    private static void PlaySound(MessageBoxImage icon)
    {
        switch (icon)
        {
            case MessageBoxImage.Error: SystemSounds.Hand.Play(); break;
            case MessageBoxImage.Warning: SystemSounds.Exclamation.Play(); break;
            case MessageBoxImage.Question: SystemSounds.Question.Play(); break;
            case MessageBoxImage.Information: SystemSounds.Asterisk.Play(); break;
        }
    }
}
