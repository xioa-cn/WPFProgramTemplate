using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;


namespace Machine.ModuleLoad.Mvvm;

/// <summary>标记需要提供按钮查询的 WPF 页面或控件类，可用于 Window、Page 和 UserControl。</summary>
/// <remarks>
/// 标记可被派生类继承，本身不创建视图、不修改按钮权限，也不自动隐藏或禁用按钮。
/// 类型发现使用 GetMarkedTypes；按钮查询需要传入已经初始化的视图实例。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class BtnAuthAttribute : Attribute
{
    /// <summary>判断类型是否为带有 BtnAuth 标记的 WPF 依赖对象类型，包括继承的标记。</summary>
    public static bool IsMarked(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return typeof(DependencyObject).IsAssignableFrom(type) &&
               type.IsDefined(typeof(BtnAuthAttribute), inherit: true);
    }

    /// <summary>查询指定程序集内已标记、非抽象且不含未绑定泛型参数的 WPF 类型。</summary>
    /// <param name="assemblies">省略时扫描当前已加载程序集；不会主动加载模块或实例化页面。</param>
    /// <returns>按程序集和类型名称排序的类型快照。</returns>
    /// <remarks>模块热加载后应重新查询；部分类型加载失败时仍返回该程序集内可成功加载的类型。</remarks>
    public static IReadOnlyList<Type> GetMarkedTypes(IEnumerable<Assembly>? assemblies = null)
    {
        var types = new HashSet<Type>();
        foreach (var assembly in (assemblies ?? AppDomain.CurrentDomain.GetAssemblies()).Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);
            if (assembly.IsDynamic) continue;
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (!type.IsAbstract && !type.ContainsGenericParameters && IsMarked(type))
                    types.Add(type);
            }
        }

        return types.OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
            .ThenBy(type => type.FullName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>查询已标记视图中当前已实例化的全部 ButtonBase 控件。</summary>
    /// <param name="view">已经执行 InitializeComponent 的视图实例，必须在其所属 UI 线程调用。</param>
    /// <returns>按钮引用的只读列表快照；未标记的视图返回空列表。</returns>
    /// <remarks>
    /// 包括普通按钮、切换按钮、复选框、单选框和已实例化的模板内部按钮；不按名称、可见性或启用状态过滤。
    /// 查询合并逻辑树与可视树，并扫描 Popup.Child、附加的 ContextMenu、ToolTip 内容，按引用去重。
    /// 不强制布局、展开菜单或实例化模板；尚未生成的 DataTemplate、虚拟化项以及独立窗口中的按钮不在结果中。
    /// 推荐在 Loaded 后查询；动态增加内容或生成模板后可重新调用。不应将返回顺序用作持久化权限标识。
    /// </remarks>
    public static IReadOnlyList<ButtonBase> GetButtons(DependencyObject view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.Dispatcher.VerifyAccess();
        return FindButtons(view, CancellationToken.None);
    }

    /// <summary>将按钮查询调度到视图所属 UI 线程，适用于从后台代码发起查询。</summary>
    /// <param name="view">已初始化并标记的视图实例。</param>
    /// <param name="cancellationToken">取消尚未开始的调度或正在进行的遍历。</param>
    /// <remarks>此方法只切换线程，不等待页面 Loaded；返回的按钮仍只能在其所属 UI 线程访问。</remarks>
    public static Task<IReadOnlyList<ButtonBase>> GetButtonsAsync(DependencyObject view,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Dispatcher.InvokeAsync(() => FindButtons(view, cancellationToken),
            DispatcherPriority.Normal, cancellationToken).Task;
    }

    /// <summary>使用显式栈遍历，避免深层嵌套页面递归溢出；同一控件只处理一次。</summary>
    private static IReadOnlyList<ButtonBase> FindButtons(DependencyObject view, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsMarked(view.GetType())) return Array.Empty<ButtonBase>();

        var result = new List<ButtonBase>();
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<DependencyObject>();
        pending.Push(view);
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            current.Dispatcher.VerifyAccess();
            if (current is ButtonBase button) result.Add(button);

            // 弹出内容不一定挂在当前可视树上，显式加入已创建的内容，不主动打开弹窗。
            if (current is Popup { Child: { } child }) pending.Push(child);
            if (current is FrameworkElement element)
            {
                if (element.ContextMenu is { } menu) pending.Push(menu);
                if (element.ToolTip is DependencyObject tooltip) pending.Push(tooltip);
            }
            else if (current is FrameworkContentElement contentElement)
            {
                if (contentElement.ContextMenu is { } menu) pending.Push(menu);
                if (contentElement.ToolTip is DependencyObject tooltip) pending.Push(tooltip);
            }

            // 逻辑树包含未显示但已创建的内容，可视树补充样式和模板生成的按钮。
            foreach (var logicalChild in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                pending.Push(logicalChild);
            if (current is not Visual && current is not Visual3D) continue;
            for (var childIndex = VisualTreeHelper.GetChildrenCount(current) - 1; childIndex >= 0; childIndex--)
                pending.Push(VisualTreeHelper.GetChild(current, childIndex));
        }

        return result.AsReadOnly();
    }

    /// <summary>单个缺失依赖不应使同一程序集内的其他可用页面无法被发现。</summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}