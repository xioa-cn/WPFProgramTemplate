using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CsxPad.Wpf.Controls;

public partial class ShortcutPresentationOverlay : UserControl
{
    private static readonly TimeSpan ChordTimeout = TimeSpan.FromSeconds(2);
    private static readonly Duration FadeInDuration = new(TimeSpan.FromMilliseconds(120));
    private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(240));
    private readonly DispatcherTimer _dismissTimer;
    private DateTime _chordDeadline = DateTime.MinValue;
    private int _presentationVersion;

    public ShortcutPresentationOverlay()
    {
        InitializeComponent();
        _dismissTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _dismissTimer.Tick += OnDismissTimerTick;
        Unloaded += OnUnloaded;
    }

    public void ProcessKey(KeyEventArgs args)
    {
        if (args.IsRepeat)
        {
            return;
        }

        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.K && modifiers == ModifierKeys.Control)
        {
            _chordDeadline = DateTime.UtcNow + ChordTimeout;
            return;
        }

        if (_chordDeadline >= DateTime.UtcNow &&
            modifiers is ModifierKeys.Control or ModifierKeys.None)
        {
            _chordDeadline = DateTime.MinValue;
            if (key == Key.C)
            {
                Show("注释当前行或所选行", "Ctrl+K, Ctrl+C");
                return;
            }

            if (key == Key.U)
            {
                Show("取消注释当前行或所选行", "Ctrl+K, Ctrl+U");
                return;
            }

            if (key == Key.D)
            {
                Show("格式化文档", "Ctrl+K, Ctrl+D");
                return;
            }
        }
        else if (key is not Key.LeftCtrl and not Key.RightCtrl)
        {
            _chordDeadline = DateTime.MinValue;
        }

        if (TryGetPresentation(key, modifiers, out var command, out var gesture))
        {
            Show(command, gesture);
        }
    }

    private void Show(string command, string gesture)
    {
        _presentationVersion++;
        _dismissTimer.Stop();
        BeginAnimation(OpacityProperty, null);
        PresentationTransform.BeginAnimation(TranslateTransform.YProperty, null);
        CommandText.Text = command;
        GestureText.Text = gesture;
        Visibility = Visibility.Visible;
        Opacity = 0;
        PresentationTransform.Y = 8;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, FadeInDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        PresentationTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, FadeInDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        _dismissTimer.Start();
    }

    private void OnDismissTimerTick(object? sender, EventArgs args)
    {
        _dismissTimer.Stop();
        var version = _presentationVersion;
        var fadeOut = new DoubleAnimation(1, 0, FadeOutDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            if (version == _presentationVersion)
            {
                Visibility = Visibility.Collapsed;
            }
        };
        BeginAnimation(OpacityProperty, fadeOut);
        PresentationTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, 8, FadeOutDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => _dismissTimer.Stop();

    private static bool TryGetPresentation(
        Key key,
        ModifierKeys modifiers,
        out string command,
        out string gesture)
    {
        (command, gesture) = (key, modifiers) switch
        {
            (Key.F5, ModifierKeys.None) => ("运行脚本", "F5"),
            (Key.F6, ModifierKeys.None) => ("调试脚本", "F6"),
            (Key.F8, ModifierKeys.None) => ("继续调试", "F8"),
            (Key.F9, ModifierKeys.None) => ("切换断点", "F9"),
            (Key.F10, ModifierKeys.None) => ("单步执行", "F10"),
            (Key.N, ModifierKeys.Control) => ("新建脚本", "Ctrl+N"),
            (Key.O, ModifierKeys.Control) => ("打开脚本", "Ctrl+O"),
            (Key.S, ModifierKeys.Control) => ("保存脚本", "Ctrl+S"),
            (Key.Z, ModifierKeys.Control) => ("撤销", "Ctrl+Z"),
            (Key.Y, ModifierKeys.Control) => ("重做", "Ctrl+Y"),
            (Key.X, ModifierKeys.Control) => ("剪切", "Ctrl+X"),
            (Key.C, ModifierKeys.Control) => ("复制", "Ctrl+C"),
            (Key.V, ModifierKeys.Control) => ("粘贴", "Ctrl+V"),
            (Key.A, ModifierKeys.Control) => ("全选", "Ctrl+A"),
            (Key.Space, ModifierKeys.Control) => ("代码补全", "Ctrl+Space"),
            (Key.Enter, ModifierKeys.Alt) => ("导入命名空间", "Alt+Enter"),
            _ => (string.Empty, string.Empty)
        };
        return command.Length > 0;
    }
}
