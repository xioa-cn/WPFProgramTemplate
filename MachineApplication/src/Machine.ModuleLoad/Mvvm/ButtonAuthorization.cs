using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Machine.ModuleLoad.Mapper;

namespace Machine.ModuleLoad.Mvvm;

/// <summary>按钮配置目录条目；没有稳定标识的按钮只展示，不允许写入授权。</summary>
public sealed record ButtonPermissionDescriptor(string Id, string Name, string ControlType, string? Key)
{
    public bool CanConfigure => Key is not null;
}

/// <summary>提供稳定按钮标识，并为已装配的 BtnAuth 页面应用非破坏性的启用状态限制。</summary>
public static class ButtonAuthorization
{
    private static readonly ConditionalWeakTable<FrameworkElement, ViewState> Views = new();
    private static readonly ConditionalWeakTable<ButtonBase, ButtonGate> Gates = new();

    /// <summary>显式业务标识，优先于 x:Name；模板按钮应使用此属性，不能依赖 PART_* 名称。</summary>
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id", typeof(string), typeof(ButtonAuthorization), new PropertyMetadata(null));

    public static string? GetId(DependencyObject element) => (string?)element.GetValue(IdProperty);
    public static void SetId(DependencyObject element, string? value) => element.SetValue(IdProperty, value);

    /// <summary>可选配置页显示名称，不参与权限标识计算，可绑定多语言文本。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached(
        "Label", typeof(string), typeof(ButtonAuthorization), new PropertyMetadata(null));

    public static string? GetLabel(DependencyObject element) => (string?)element.GetValue(LabelProperty);
    public static void SetLabel(DependencyObject element, string? value) => element.SetValue(LabelProperty, value);

    static ButtonAuthorization()
    {
        // 延迟生成的模板按钮在 Loaded 时补充授权，无须持续遍历页面的 LayoutUpdated。
        EventManager.RegisterClassHandler(typeof(ButtonBase), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnButtonLoaded));
    }

    /// <summary>按程序集简单名称、页面完整类型名及按钮业务标识生成固定长度权限键。</summary>
    public static string GetPermissionKey(Type pageType, string buttonId)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(buttonId);
        var pageId = $"{pageType.Assembly.GetName().Name}:{pageType.FullName}";
        return $"button:{Hash(pageId)}:{Hash(buttonId.Trim())}";
    }

    /// <summary>查询已实例化按钮；相同显式标识共用一项授权，不使用位置或显示文字生成键。</summary>
    public static IReadOnlyList<ButtonPermissionDescriptor> Describe(DependencyObject view)
    {
        var result = new List<ButtonPermissionDescriptor>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var button in BtnAuthAttribute.GetButtons(view))
        {
            var descriptor = DescribeButton(view.GetType(), button);
            if (descriptor.Key is null || keys.Add(descriptor.Key)) result.Add(descriptor);
        }
        return result;
    }

    /// <summary>挂载到已初始化页面；重复装配不会重复订阅权限事件。</summary>
    public static void Attach(DependencyObject view, PermissionService permissions)
    {
        if (view is not FrameworkElement element || !BtnAuthAttribute.IsMarked(view.GetType())) return;
        element.Dispatcher.VerifyAccess();
        Views.GetValue(element, current => new ViewState(current, permissions)).Refresh();
    }

    /// <summary>优先使用显式标识，其次使用非模板按钮的名称；不把模板内部控件误当成业务按钮。</summary>
    private static ButtonPermissionDescriptor DescribeButton(Type pageType, ButtonBase button)
    {
        var identifier = GetId(button)?.Trim();
        if (string.IsNullOrWhiteSpace(identifier))
            identifier = button.TemplatedParent is null ? button.Name : "";
        var name = GetLabel(button);
        if (string.IsNullOrWhiteSpace(name)) name = AutomationProperties.GetName(button);
        if (string.IsNullOrWhiteSpace(name)) name = button.Content as string;
        if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(identifier) ? button.GetType().Name : identifier;
        return new(identifier ?? "", name, button.GetType().Name,
            string.IsNullOrWhiteSpace(identifier) ? null : GetPermissionKey(pageType, identifier));
    }

    /// <summary>沿逻辑树和可视树寻找最近的已装配页面，支持运行时新增的按钮。</summary>
    private static void OnButtonLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not ButtonBase button) return;
        DependencyObject? current = button;
        while (current is not null)
        {
            if (current is FrameworkElement element && Views.TryGetValue(element, out var state))
            {
                state.Apply(button);
                return;
            }
            current = LogicalTreeHelper.GetParent(current) ??
                      (current is Visual ? VisualTreeHelper.GetParent(current) : null);
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>使用弱事件订阅，缓存页面销毁后不会被单例权限服务持有。</summary>
    private sealed class ViewState
    {
        private readonly FrameworkElement _view;
        private readonly PermissionService _permissions;

        public ViewState(FrameworkElement view, PermissionService permissions)
        {
            _view = view;
            _permissions = permissions;
            view.Loaded += (_, _) => Refresh();
            WeakEventManager<PermissionService, EventArgs>.AddHandler(permissions, nameof(permissions.Changed), OnChanged);
            WeakEventManager<PermissionService, EventArgs>.AddHandler(permissions, nameof(permissions.ButtonPermissionsChanged), OnChanged);
        }

        public void Refresh()
        {
            foreach (var button in BtnAuthAttribute.GetButtons(_view)) Apply(button);
        }

        public void Apply(ButtonBase button)
        {
            var descriptor = DescribeButton(_view.GetType(), button);
            if (descriptor.Key is null) return;
            Gates.GetValue(button, _ => new ButtonGate()).SetAllowed(button, _permissions.Allows(descriptor.Key));
        }

        private void OnChanged(object? sender, EventArgs args)
        {
            if (_view.Dispatcher.CheckAccess()) Refresh();
            else if (!_view.Dispatcher.HasShutdownStarted) _view.Dispatcher.InvokeAsync(Refresh);
        }
    }

    /// <summary>以可移除的布尔动画限制 IsEnabled，撤销后恢复原有绑定、样式和命令 CanExecute。</summary>
    private sealed class ButtonGate
    {
        private AnimationClock? _deniedClock;

        public void SetAllowed(ButtonBase button, bool allowed)
        {
            if (allowed)
            {
                _deniedClock?.Controller?.Remove();
                _deniedClock = null;
                return;
            }
            if (_deniedClock is not null) return;
            var animation = new BooleanAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.Zero), FillBehavior = FillBehavior.HoldEnd };
            animation.KeyFrames.Add(new DiscreteBooleanKeyFrame(false, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            _deniedClock = (AnimationClock)animation.CreateClock(true);
            button.ApplyAnimationClock(UIElement.IsEnabledProperty, _deniedClock, HandoffBehavior.Compose);
        }
    }
}
