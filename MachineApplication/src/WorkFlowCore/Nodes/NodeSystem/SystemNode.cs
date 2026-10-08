using System.Collections;
using System.Globalization;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class SystemNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private readonly List<(XTNodeOption Port, Func<object?> DefaultValue)> _inputs = [];
    private readonly HashSet<XTNodeOption> _received = [];

    protected SystemNode(string title, Type outputType)
    {
        SetNodeTypeTitle(title);
        TitleColor = Color.FromRgb(86, 154, 180);
        Output = OutputOptions.Add("结果", outputType, false);
        Completed = OutputOptions.Add("完成", typeof(object), false);
    }

    public XTNodeOption Output { get; }
    public XTNodeOption Completed { get; }

    protected XTNodeOption AddInput(string name, Func<object?> defaultValue)
    {
        var port = InputOptions.Add(name, typeof(object), true);
        port.HasDefaultValue = true;
        port.DataTransfer += (_, args) =>
        {
            port.Data = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null;
            if (args.Status == ConnectionStatus.Connected && args.IsSponsor) _received.Add(port);
            else _received.Remove(port);
        };
        _inputs.Add((port, defaultValue));
        return port;
    }

    public EditorNodeReadinessResult CanExecute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var values = ReadInputs(true);
            if (values is not null) ValidateValues(values);
            return EditorNodeReadinessResult.Ready();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EditorNodeReadinessResult.NotReady(exception.Message);
        }
    }

    public virtual EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ClearOutputs();
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var values = ReadInputs(false)!;
            ValidateValues(values);
            var result = Task.Run(() => RunAsync(values, context.CancellationToken), context.CancellationToken)
                .GetAwaiter().GetResult();
            context.CancellationToken.ThrowIfCancellationRequested();
            RuntimeText = result switch
            {
                null => "null",
                ICollection collection => $"{collection.Count} 项",
                _ => Convert.ToString(result, CultureInfo.InvariantCulture) ?? "null"
            };
            if (RuntimeText.Length > 120) RuntimeText = RuntimeText[..120] + "…";
            Output.TransferData(result);
            Completed.TransferData(new EditorFlowSignal(context.ExecutionId));
            return EditorNodeExecutionResult.Success($"{Title}完成。", Output, Completed);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            ClearOutputs();
            RuntimeText = "已取消";
            throw;
        }
        catch (Exception exception)
        {
            ClearOutputs();
            RuntimeText = exception is OperationCanceledException ? "操作超时" : "执行失败";
            return EditorNodeExecutionResult.Failure(exception is OperationCanceledException ? "操作超时。" : exception.Message);
        }
    }

    protected object?[]? ReadInputs(bool allowPending)
    {
        var values = new object?[_inputs.Count];
        var pending = false;
        for (var index = 0; index < _inputs.Count; index++)
        {
            var (port, defaultValue) = _inputs[index];
            if (port.ConnectionCount > 0 && port.Data is null && !_received.Contains(port))
            {
                if (!allowPending) throw new InvalidOperationException($"输入“{port.Text}”尚未收到数据。");
                pending = true;
                continue;
            }
            values[index] = port.ConnectionCount > 0 || _received.Contains(port) || port.Data is not null ? port.Data : defaultValue();
        }
        return pending ? null : values;
    }

    private void ClearOutputs()
    {
        foreach (var port in GetOutputOptions()) port.Data = null;
    }

    protected virtual void ValidateSettings() { }
    protected virtual void ValidateValues(object?[] values) { }
    protected virtual object? Run(object?[] values, CancellationToken cancellationToken) => throw new NotSupportedException();
    protected virtual Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken) => Task.FromResult(Run(values, cancellationToken));
}
