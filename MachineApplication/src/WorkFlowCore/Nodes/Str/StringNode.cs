using System.Globalization;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Str;

public abstract class StringNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private readonly List<(XTNodeOption Port, Type ValueType, Func<object?> DefaultValue)> _inputs = [];

    protected StringNode(string title, Type outputType, string description)
    {
        SetNodeTypeTitle(title);
        TitleColor = Color.FromRgb(78, 178, 180);
        Output = OutputOptions.Add($"结果 · {outputType.Name}", outputType, false);
        Output.Description = description;
    }

    public XTNodeOption Output { get; }

    protected XTNodeOption AddInput<T>(string name, Func<T> defaultValue)
    {
        var input = InputOptions.Add(name, typeof(object), true);
        input.HasDefaultValue = true;
        input.Description = $"需要 {typeof(T).Name}；未连接且未赋值时使用同名属性的默认值。";
        input.DataTransfer += (_, args) =>
            input.Data = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null;
        _inputs.Add((input, typeof(T), () => defaultValue()));
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
            if (values is not null) ValidateValues(values);
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
        Output.Data = null;
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var values = ReadInputs(false)!;
            ValidateValues(values);
            var result = Calculate(values);
            context.CancellationToken.ThrowIfCancellationRequested();
            var preview = result is string[] array
                ? $"{array.Length} 项：{string.Join(" | ", array.Take(8))}"
                : Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
            RuntimeText = preview.Length > 160 ? preview[..160] + "…" : preview;
            Output.TransferData(result);
            return EditorNodeExecutionResult.Success($"{Title}：{RuntimeText}", Output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            RuntimeText = "执行失败";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    private object[]? ReadInputs(bool allowPending)
    {
        var values = new object[_inputs.Count];
        var pending = false;
        for (var index = 0; index < _inputs.Count; index++)
        {
            var (port, valueType, defaultValue) = _inputs[index];
            var value = port.Data;
            if (value is null && port.ConnectionCount > 0)
            {
                if (!allowPending) throw new InvalidOperationException($"输入“{port.Text}”尚未收到数据。");
                pending = true;
                continue;
            }
            value ??= defaultValue();
            if (value is null && valueType == typeof(string)) value = string.Empty;
            if (value is null || !valueType.IsInstanceOfType(value))
                throw new InvalidOperationException($"输入“{port.Text}”必须为 {valueType.Name}，实际为 {value?.GetType().Name ?? "null"}。");
            values[index] = value;
        }
        return pending ? null : values;
    }

    protected static StringComparison Comparison(bool ignoreCase) =>
        ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    protected virtual void ValidateSettings() { }
    protected virtual void ValidateValues(object[] values) { }
    protected abstract object Calculate(object[] values);
}

public enum StringCaseMode
{
    Upper,
    Lower
}
