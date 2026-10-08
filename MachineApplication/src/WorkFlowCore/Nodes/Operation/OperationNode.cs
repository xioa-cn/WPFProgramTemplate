using System.Collections;
using System.Globalization;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public abstract class OperationNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private readonly List<(XTNodeOption Port, Func<object?> DefaultValue)> _inputs = [];
    private readonly HashSet<XTNodeOption> _received = [];

    protected OperationNode(string title, Type outputType)
    {
        SetNodeTypeTitle(title);
        TitleColor = Color.FromRgb(90, 155, 205);
        Output = OutputOptions.Add("结果", outputType, false);
    }

    public XTNodeOption Output { get; }

    protected XTNodeOption AddInput(string name, Func<object?> defaultValue)
    {
        var input = InputOptions.Add(name, typeof(object), true);
        input.HasDefaultValue = true;
        input.Description = "未连接时使用同名属性的默认值；已连接时使用上游传入值（包括 null）。";
        input.DataTransfer += (_, args) =>
        {
            if (args.Status == ConnectionStatus.Connected)
            {
                input.Data = args.TargetOption.Data;
                if (args.IsSponsor) _received.Add(input);
                else _received.Remove(input);
            }
            else
            {
                input.Data = null;
                _received.Remove(input);
            }
        };
        _inputs.Add((input, defaultValue));
        return input;
    }

    public EditorNodeReadinessResult CanExecute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var values = ReadInputs(true);
            if (values is not null) Evaluate(values, context, true);
            return EditorNodeReadinessResult.Ready();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EditorNodeReadinessResult.NotReady(exception.Message);
        }
    }

    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ClearOutputs();
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var value = Evaluate(ReadInputs(false)!, context, false);
            context.CancellationToken.ThrowIfCancellationRequested();
            RuntimeText = value switch
            {
                null => "null",
                ICollection collection => $"{collection.Count} 项",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null"
            };
            if (RuntimeText.Length > 120) RuntimeText = RuntimeText[..120] + "…";
            var active = Publish(value, context);
            return EditorNodeExecutionResult.Success($"{Title}：{RuntimeText}", active);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ClearOutputs();
            RuntimeText = "执行失败";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    private object?[]? ReadInputs(bool allowPending)
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
            values[index] = port.ConnectionCount > 0 || _received.Contains(port) || port.Data is not null
                ? port.Data : defaultValue();
        }
        return pending ? null : values;
    }

    private void ClearOutputs()
    {
        foreach (var output in GetOutputOptions()) output.Data = null;
    }

    protected virtual XTNodeOption[] Publish(object? value, EditorExecutionContext context)
    {
        Output.TransferData(value);
        return [Output];
    }

    protected virtual void ValidateSettings() { }
    protected abstract object? Evaluate(object?[] values, EditorExecutionContext context, bool preview);
}
