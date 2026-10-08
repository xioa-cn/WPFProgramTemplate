using System.Collections.Concurrent;

namespace ST.Library.UI.NodeEditor;

public interface IEditorExecutableNode
{
    EditorNodeExecutionResult Execute(EditorExecutionContext context);
}

public interface IEditorLoggableNode
{
    bool EnableExecutionLog { get; }
}

public interface IEditorStartNode
{
}

public interface IEditorNodeReadiness
{
    EditorNodeReadinessResult CanExecute(EditorExecutionContext context);
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorExecutionContext
{
    private readonly object _activatedInputsRoot = new object();

    private readonly Dictionary<XTNodeOption, Queue<object>>
        _activatedInputs =
            new Dictionary<XTNodeOption, Queue<object>>();

    public EditorExecutionContext() : this(CancellationToken.None)
    {
    }

    public EditorExecutionContext(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        ExecutionId = Guid.NewGuid();
        Items = new ConcurrentDictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);
        StopOnFailure = true;
        MaxSteps = 10000;
    }

    public Guid ExecutionId { get; private set; }
    public CancellationToken CancellationToken { get; }

    public Func<int> NodeTransitionDelayMillisecondsProvider { get; set; }
    public Func<int> SuccessfulCycleDelayMillisecondsProvider { get; set; }
    public Func<int> FailedCycleDelayMillisecondsProvider { get; set; }
    public Action<string, string> RecipeChanged { get; set; }
    public Action<EditorExecutionResult> FlowCycleCompleted { get; set; }
    public IDictionary<string, object> Items { get; }
    public bool StopOnFailure { get; set; }
    public int MaxSteps { get; set; }

    internal Action<string> NodeProgressReporter { get; set; }

    // 由执行节点同步调用；编辑器将事件marshalling到其UI线程。
    public void ReportNodeProgress(string message)
    {
        NodeProgressReporter?.Invoke(message ?? string.Empty);
    }

    public bool IsInputActivated(XTNodeOption input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        lock (_activatedInputsRoot)
        {
            Queue<object> values;
            return _activatedInputs.TryGetValue(input, out values) &&
                   values.Count > 0;
        }
    }


    internal void BeginExecution()
    {
        ExecutionId = Guid.NewGuid();
        lock (_activatedInputsRoot)
            _activatedInputs.Clear();
    }

    internal void MarkInputActivated(XTNodeOption input, object value)
    {
        if (input == null) return;
        lock (_activatedInputsRoot)
        {
            Queue<object> values;
            if (!_activatedInputs.TryGetValue(input, out values))
            {
                values = new Queue<object>();
                _activatedInputs.Add(input, values);
            }

            values.Enqueue(value);
        }
    }

    internal void PrepareNodeInputs(XTNode node)
    {
        if (node == null) return;
        XTNodeOption[] inputs = node.GetInputOptions() ??
                                new XTNodeOption[0];
        lock (_activatedInputsRoot)
        {
            foreach (XTNodeOption input in inputs)
            {
                Queue<object> values;
                if (input != null &&
                    _activatedInputs.TryGetValue(input, out values) &&
                    values.Count > 0)
                    input.Data = values.Peek();
            }
        }
    }

    internal void ConsumeNodeInputs(XTNode node)
    {
        if (node == null) return;
        XTNodeOption[] inputs = node.GetInputOptions() ??
                                new XTNodeOption[0];
        lock (_activatedInputsRoot)
        {
            foreach (XTNodeOption input in inputs)
            {
                Queue<object> values;
                if (input == null ||
                    !_activatedInputs.TryGetValue(input, out values) ||
                    values.Count == 0)
                    continue;
                values.Dequeue();
                if (values.Count == 0) _activatedInputs.Remove(input);
            }
        }
    }

    internal bool HasPendingInputs(XTNode node)
    {
        if (node == null) return false;
        XTNodeOption[] inputs = node.GetInputOptions() ??
                                new XTNodeOption[0];
        lock (_activatedInputsRoot)
        {
            foreach (XTNodeOption input in inputs)
            {
                Queue<object> values;
                if (input != null &&
                    _activatedInputs.TryGetValue(input, out values) &&
                    values.Count > 0)
                    return true;
            }

            return false;
        }
    }

    internal int GetNodeTransitionDelayMilliseconds()
    {
        int delay = NodeTransitionDelayMillisecondsProvider == null
            ? 0
            : NodeTransitionDelayMillisecondsProvider();
        return Math.Max(0, delay);
    }

    internal int GetSuccessfulCycleDelayMilliseconds()
    {
        int delay = SuccessfulCycleDelayMillisecondsProvider == null
            ? 50
            : SuccessfulCycleDelayMillisecondsProvider();
        return Math.Max(0, delay);
    }

    internal int GetFailedCycleDelayMilliseconds()
    {
        int delay = FailedCycleDelayMillisecondsProvider == null
            ? 5000
            : FailedCycleDelayMillisecondsProvider();
        return Math.Max(0, delay);
    }

    internal EditorExecutionContext CreateCycleContext()
    {
        return new EditorExecutionContext(CancellationToken)
        {
            NodeTransitionDelayMillisecondsProvider =
                NodeTransitionDelayMillisecondsProvider,
            SuccessfulCycleDelayMillisecondsProvider =
                SuccessfulCycleDelayMillisecondsProvider,
            FailedCycleDelayMillisecondsProvider =
                FailedCycleDelayMillisecondsProvider,
            RecipeChanged = RecipeChanged,
            FlowCycleCompleted = FlowCycleCompleted,
            StopOnFailure = StopOnFailure,
            MaxSteps = MaxSteps
        };
    }

    internal void ReportFlowCycle(EditorExecutionResult result)
    {
        if (result != null) FlowCycleCompleted?.Invoke(result);
    }

    public void NotifyRecipeChanged(string productionKey, string recipeName)
    {
        RecipeChanged?.Invoke(productionKey, recipeName);
    }

    private static string RequireKey(string key, string deviceName)
    {
        string normalizedKey = (key ?? string.Empty).Trim();
        if (normalizedKey.Length == 0)
            throw new InvalidOperationException(deviceName + " Key 不能为空。");
        return normalizedKey;
    }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorFlowSignal
{
    public EditorFlowSignal(Guid executionId)
    {
        ExecutionId = executionId;
        CreatedAt = DateTime.Now;
    }

    public Guid ExecutionId { get; }
    public DateTime CreatedAt { get; }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorNodeReadinessResult
{
    private EditorNodeReadinessResult(bool isReady, string message)
    {
        IsReady = isReady;
        Message = message ?? string.Empty;
    }

    public bool IsReady { get; }
    public string Message { get; }

    public static EditorNodeReadinessResult Ready()
    {
        return new EditorNodeReadinessResult(true, string.Empty);
    }

    public static EditorNodeReadinessResult NotReady(string message)
    {
        return new EditorNodeReadinessResult(false, message);
    }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorNodeExecutionResult
{
    private EditorNodeExecutionResult(bool isSuccess, string message, XTNodeOption[] activeOutputs)
    {
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        ActiveOutputs = activeOutputs ?? new XTNodeOption[0];
    }

    public bool IsSuccess { get; }
    public string Message { get; }
    public XTNodeOption[] ActiveOutputs { get; }

    public static EditorNodeExecutionResult Success(string message,
        params XTNodeOption[] activeOutputs)
    {
        return new EditorNodeExecutionResult(true, message, activeOutputs);
    }

    public static EditorNodeExecutionResult Failure(string message)
    {
        return new EditorNodeExecutionResult(false, message, new XTNodeOption[0]);
    }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorExecutionStep
{
    internal EditorExecutionStep(int sequence, XTNode node, bool isSuccess,
        string message, TimeSpan elapsed, XTNodeOption[] activeOutputs)
    {
        Sequence = sequence;
        NodeGuid = node.Guid;
        NodeTitle = node.Title;
        NodeType = node.GetType().FullName;
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        Elapsed = elapsed;
        ActiveOutputs = activeOutputs ?? new XTNodeOption[0];
        EnableExecutionLog = !(node is IEditorLoggableNode loggable) ||
                             loggable.EnableExecutionLog;
    }

    public int Sequence { get; }
    public Guid NodeGuid { get; }
    public string NodeTitle { get; }
    public string NodeType { get; }
    public bool IsSuccess { get; }
    public string Message { get; }
    public TimeSpan Elapsed { get; }
    public XTNodeOption[] ActiveOutputs { get; }
    public bool EnableExecutionLog { get; }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorNodeExecutionEventArgs : EventArgs
{
    internal EditorNodeExecutionEventArgs(Guid flowStartNodeGuid, XTNode node,
        bool isCompleted, bool isSuccess, string message,
        string runtimeValueText)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        FlowStartNodeGuid = flowStartNodeGuid;
        NodeGuid = node.Guid;
        NodeTitle = node.Title;
        NodeType = node.GetType().FullName;
        IsCompleted = isCompleted;
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        RuntimeValueText = runtimeValueText ?? string.Empty;
    }

    public Guid FlowStartNodeGuid { get; }
    public Guid NodeGuid { get; }
    public string NodeTitle { get; }
    public string NodeType { get; }
    public bool IsCompleted { get; }
    public bool IsSuccess { get; }
    public string Message { get; }
    public string RuntimeValueText { get; }
}

// 作者：xioa
// 作者邮箱：1327916255@qq.com
public sealed class EditorExecutionResult
{
    internal EditorExecutionResult(Guid executionId, bool isSuccess,
        bool isCanceled, string message, EditorExecutionStep[] steps)
    {
        ExecutionId = executionId;
        IsSuccess = isSuccess;
        IsCanceled = isCanceled;
        Message = message ?? string.Empty;
        Steps = steps ?? new EditorExecutionStep[0];
    }

    public Guid ExecutionId { get; }
    public bool IsSuccess { get; }
    public bool IsCanceled { get; }
    public string Message { get; }
    public EditorExecutionStep[] Steps { get; }
}
