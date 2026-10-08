using System.Globalization;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Math;

public abstract class MathNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private readonly List<(XTNodeOption Port, Func<double> DefaultValue)> _inputs = [];

    protected MathNode(string title)
    {
        SetNodeTypeTitle(title);
        TitleColor = Color.FromRgb(230, 163, 68);
        Output = OutputOptions.Add("结果 · Double", typeof(double), false);
    }

    public XTNodeOption Output { get; }

    protected void SetOutputDescription(string expression, string explanation)
    {
        SetOptionText(Output, expression);
        Output.Description = explanation;
    }

    protected XTNodeOption AddInput(string name, Func<double> defaultValue)
    {
        var input = InputOptions.Add(name, typeof(object), true);
        input.HasDefaultValue = true;
        input.DataTransfer += (_, args) =>
        {
            input.Data = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null;
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
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            var values = ReadInputs(false)!;
            ValidateValues(values);
            var result = Calculate(values);
            if (!double.IsFinite(result)) throw new ArithmeticException("运算结果超出有限数值范围。");
            Output.TransferData(result);
            RuntimeText = result.ToString("G17", CultureInfo.InvariantCulture);
            return EditorNodeExecutionResult.Success($"{Title}：{RuntimeText}", Output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            RuntimeText = "运算失败";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    private double[]? ReadInputs(bool allowPending)
    {
        var values = new double[_inputs.Count];
        var pending = false;
        for (var index = 0; index < _inputs.Count; index++)
        {
            var (port, defaultValue) = _inputs[index];
            var value = port.Data;
            if (value is null && port.ConnectionCount > 0)
            {
                if (!allowPending) throw new InvalidOperationException($"输入“{port.Text}”尚未收到数值。");
                pending = true;
                continue;
            }
            values[index] = value is null ? defaultValue() : value switch
            {
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                    => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"输入“{port.Text}”必须为数值类型，实际为 {value.GetType().Name}。")
            };
            if (!double.IsFinite(values[index]))
                throw new ArgumentOutOfRangeException(port.Text, "输入必须为有限数值。");
        }
        return pending ? null : values;
    }

    protected virtual void ValidateValues(double[] values)
    {
    }

    protected abstract double Calculate(double[] values);
}
