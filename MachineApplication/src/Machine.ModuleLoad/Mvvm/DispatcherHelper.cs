using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.Mvvm;

/// <summary>
/// WPF UI 线程调度辅助类。
/// </summary>
/// <remarks>
/// <para>
/// 该类只负责把代码切换到 WPF UI 线程，不负责创建后台线程。
/// 耗时的文件、数据库或网络操作应在调用方异步执行，完成后再使用本类更新界面。
/// </para>
/// <para>
/// <see cref="Post(Action, DispatcherPriority, CancellationToken)"/> 用于不等待结果的投递；
/// <see cref="InvokeAsync(Action, DispatcherPriority, CancellationToken)"/> 和异步委托重载用于等待执行完成。
/// </para>
/// </remarks>
public static class DispatcherHelper
{
    private static readonly object SyncRoot = new();
    private static Dispatcher? _uiDispatcher;

    /// <summary>
    /// 将当前 WPF 调度器初始化到依赖注入注册流程中。
    /// </summary>
    /// <remarks>
    /// 应在 STA 启动线程调用。当前项目在创建 WPF 应用容器前调用此方法，
    /// 因此这里不能强制要求 <see cref="Application.Current"/> 已经存在。
    /// 本方法仅初始化静态辅助类，不向服务集合注册 Dispatcher 服务。
    /// </remarks>
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Initialize();
        return services;
    }

    /// <summary>
    /// 获取当前注册的 UI 调度器。
    /// </summary>
    /// <exception cref="InvalidOperationException">尚未初始化，或调度器已停止接受操作。</exception>
    public static Dispatcher UIDispatcher => GetDispatcher();

    /// <summary>
    /// 将操作投递到 UI 线程，不等待操作执行完成。
    /// </summary>
    /// <param name="action">需要在 UI 线程执行的操作。</param>
    /// <param name="priority">Dispatcher 调度优先级。</param>
    /// <param name="cancellationToken">取消尚未执行的调度操作。</param>
    /// <returns>可用于观察执行状态和异常的 DispatcherOperation。</returns>
    /// <remarks>
    /// 即使当前已在 UI 线程也始终排队，不会立即执行。
    /// 操作异常保存在返回对象的 Task 中，需要等待该 Task 或显式观察异常。
    /// </remarks>
    public static DispatcherOperation Post(Action action,
        DispatcherPriority priority = DispatcherPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = GetDispatcher();
        return dispatcher.InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>
    /// 已在 UI 线程时立即执行，否则通过 BeginInvoke 投递。
    /// </summary>
    /// <remarks>
    /// 为保持旧行为，空操作会被忽略。后台投递的执行异常走 Dispatcher 的异常处理流程；
    /// 需要由调用方捕获执行异常时，请改用 InvokeAsync 并等待返回任务。
    /// </remarks>
    public static void CheckBeginInvokeOnUI(Action? action)
    {
        if (action is null) return;

        var dispatcher = GetDispatcher();
        if (dispatcher.CheckAccess())
            action();
        else
            _ = dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// 在 UI 线程执行同步操作，并等待操作完成。
    /// </summary>
    /// <param name="action">短时同步 UI 操作，不要传入 async void 委托。</param>
    /// <param name="priority">跨线程排队时的优先级；UI 线程上的调用会立即执行。</param>
    /// <param name="cancellationToken">仅用于执行开始前的取消，不能中断已经运行的同步代码。</param>
    /// <returns>包含执行完成、异常或取消状态的任务。</returns>
    public static async Task InvokeAsync(Action action,
        DispatcherPriority priority = DispatcherPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = GetDispatcher();
        cancellationToken.ThrowIfCancellationRequested();

        // 在 UI 线程直接执行，避免不必要的排队；async 方法会把执行异常放入返回任务。
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>
    /// 在 UI 线程执行同步操作，并等待操作完成。
    /// </summary>
    /// <remarks>保留此方法作为原有 API 的兼容入口。</remarks>
    public static Task CheckBeginInvokeOnUIAsync(Action action)
        => InvokeAsync(action);

    /// <summary>
    /// 在 UI 线程执行异步操作，并等待异步操作本身完成。
    /// </summary>
    /// <remarks>
    /// 使用此重载可以避免把 async lambda 传给 Action 后变成 async void，
    /// 从而保证异常和取消状态能够返回给调用方。
    /// 取消令牌仅取消尚未开始的调度；异步操作开始后若需要取消，应使用接收取消令牌的委托重载。
    /// </remarks>
    /// <param name="function">在 UI 线程启动且需要完整等待的异步操作。</param>
    /// <param name="priority">跨线程排队时的优先级。</param>
    /// <param name="cancellationToken">执行开始前使用的取消令牌。</param>
    /// <returns>等待内部异步任务完成的任务，而不只是等待委托被调用。</returns>
    public static async Task InvokeAsync(Func<Task> function,
        DispatcherPriority priority = DispatcherPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        var dispatcher = GetDispatcher();
        cancellationToken.ThrowIfCancellationRequested();

        if (dispatcher.CheckAccess())
        {
            await function();
            return;
        }

        var operation = dispatcher.InvokeAsync(function, priority, cancellationToken);
        // 调度返回 Task<Task>：外层表示委托已启动，内层才表示实际异步操作完成。
        await operation.Task.Unwrap();
    }

    /// <summary>
    /// 在 UI 线程启动支持协作取消的异步操作，并等待操作完成。
    /// </summary>
    /// <param name="function">接收取消令牌的异步操作。</param>
    /// <param name="priority">跨线程排队时的优先级。</param>
    /// <param name="cancellationToken">同时传给 Dispatcher 和业务操作的取消令牌。</param>
    /// <remarks>操作开始后由委托主动检查或向下传递令牌，不会强制终止正在执行的代码。</remarks>
    public static async Task InvokeAsync(Func<CancellationToken, Task> function,
        DispatcherPriority priority = DispatcherPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        await InvokeAsync(() => function(cancellationToken), priority, cancellationToken);
    }

    /// <summary>
    /// 在 UI 线程执行异步操作并等待完成。
    /// </summary>
    public static Task CheckBeginInvokeOnUIAsync(Func<Task> function)
        => InvokeAsync(function);

    /// <summary>
    /// 向 UI 线程排队执行操作。
    /// </summary>
    /// <remarks>保留 BeginInvoke 的异常处理语义；名称中的 Async 不表示在后台线程运行。</remarks>
    public static DispatcherOperation RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return GetDispatcher().BeginInvoke(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// 使用已注册调度器或 WPF 应用调度器初始化；应用尚未创建时绑定当前 STA 启动线程。
    /// </summary>
    /// <remarks>尚无 Application 时，必须由真正的 UI 启动线程调用，不能从线程池初始化。</remarks>
    public static void Initialize() => InitializeCore(null);

    /// <summary>
    /// 显式绑定指定的 STA UI 调度器，同一调度器允许重复初始化。
    /// </summary>
    /// <param name="dispatcher">UI 线程对应的调度器，不能为空或已经关闭。</param>
    /// <exception cref="InvalidOperationException">已有其他有效绑定，或指定线程不适合作为 UI 线程。</exception>
    public static void Initialize(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        InitializeCore(dispatcher);
    }

    /// <summary>串行处理初始化，防止并发调用覆盖已有的有效 UI 线程绑定。</summary>
    private static void InitializeCore(Dispatcher? dispatcher)
    {
        lock (SyncRoot)
        {
            var existing = _uiDispatcher;
            if (existing is not null && IsAvailable(existing))
            {
                if (dispatcher is not null && !ReferenceEquals(existing, dispatcher))
                    throw new InvalidOperationException("DispatcherHelper 已经绑定到其他 UI 调度器。请勿重复初始化。");
                return;
            }

            dispatcher ??= Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                // CurrentDispatcher 会按需创建调度器，必须先排除没有 UI 消息循环的线程池线程。
                if (Thread.CurrentThread.IsThreadPoolThread || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                    throw new InvalidOperationException("WPF 应用尚未创建，请在 STA 启动线程中初始化 UI 调度器。");

                dispatcher = Dispatcher.CurrentDispatcher;
            }

            if (!IsAvailable(dispatcher))
                throw new InvalidOperationException("不能绑定到已经关闭的 UI 调度器。");
            if (dispatcher.Thread.IsThreadPoolThread || dispatcher.Thread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("UI 调度器必须属于 STA 线程，不能绑定线程池线程。");

            Volatile.Write(ref _uiDispatcher, dispatcher);
        }
    }

    /// <summary>
    /// 清除当前调度器引用，主要用于应用关闭或测试环境重置。
    /// </summary>
    /// <remarks>仅清除绑定，不关闭 Dispatcher，也不会取消已经取得调度器引用或提交的操作。</remarks>
    public static void Reset()
    {
        lock (SyncRoot)
            Volatile.Write(ref _uiDispatcher, null);
    }

    /// <summary>获取并校验同一份调度器快照，避免 Reset 与后续静态属性读取之间产生空引用竞争。</summary>
    private static Dispatcher GetDispatcher()
    {
        // 后续只使用局部引用；即使其他线程 Reset，本次调用也不会再次读到空的静态字段。
        var dispatcher = Volatile.Read(ref _uiDispatcher);
        if (dispatcher is null)
        {
            throw new InvalidOperationException(
                "DispatcherHelper 尚未初始化，请在 STA 启动线程中调用 DispatcherHelper.Initialize()。");
        }

        if (!IsAvailable(dispatcher))
            throw new InvalidOperationException("UI 调度器正在关闭，不能继续提交 UI 操作。");

        return dispatcher;
    }

    /// <summary>检查线程存活和调度器关闭状态；真正提交时的关闭竞争仍由 Dispatcher 自身处理。</summary>
    private static bool IsAvailable(Dispatcher dispatcher)
        => dispatcher.Thread.IsAlive && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished;
}
