using System.ComponentModel;
using System.Text;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Data;

/// <summary>输出固定值的数据源节点，可通过类型属性控制输出端口的数据类型。</summary>
[XTNode("数据", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "输出一个不会被流程修改的固定值，支持字符串、整数、小数、布尔值和时间。")]
[DisplayName("常量")]
public sealed class ConstDataNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private WorkflowDataValueType _valueType = WorkflowDataValueType.String;
    private string _value = string.Empty;

    /// <summary>创建常量节点并初始化字符串输出端口。</summary>
    public ConstDataNode()
    {
        // SetNodeTypeTitle("常量");
        TitleColor = Color.FromRgb(212, 10, 235);
        Output = OutputOptions.Add("值", typeof(string), false);
        RefreshOutputValue();
    }

    [XTNodeProperty("数据类型", "修改前先断开连接；旧值能转换时保留，否则重置为对应类型的默认值。")]
    public WorkflowDataValueType ValueType
    {
        get => _valueType;
        set
        {
            if (_valueType == value) return;
            VerifyAccess();
            var dataType = WorkflowDataValueConverter.GetClrType(value);
            if (Output.ConnectionCount > 0) throw new InvalidOperationException("请先断开输出连接，再修改数据类型。");
            var text = _value;
            object? parsed;
            try
            {
                parsed = WorkflowDataValueConverter.Parse(text, value);
            }
            catch (FormatException)
            {
                text = WorkflowDataValueConverter.GetDefaultText(value);
                parsed = WorkflowDataValueConverter.Parse(text, value);
            }
            // 全部校验通过后才提交类型、文本与端口值，失败时保持原节点配置。
            Output.SetDataType(dataType);
            _valueType = value;
            _value = text;
            Output.Data = parsed;
            UpdatePreview();
        }
    }

    [XTNodeProperty("常量值", "按数据类型解析；例如 Boolean 使用 true 或 false，DateTime 使用 ISO 时间文本。")]
    public string Value
    {
        get => _value;
        set
        {
            VerifyAccess();
            var text = value ?? string.Empty;
            var parsed = WorkflowDataValueConverter.Parse(text, _valueType);
            _value = text;
            Output.Data = parsed;
            UpdatePreview();
        }
    }

    public XTNodeOption Output { get; }

    /// <summary>检查常量文本能否按当前类型解析，供执行器在运行前快速校验。</summary>
    public EditorNodeReadinessResult CanExecute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            _ = ParseValue();
            return EditorNodeReadinessResult.Ready();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EditorNodeReadinessResult.NotReady(exception.Message);
        }
    }

    /// <summary>解析并广播常量值，后续节点通过输出端口接收同一实例。</summary>
    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            var value = ParseValue();
            Output.TransferData(value);
            return EditorNodeExecutionResult.Success($"常量“{Value}”已输出。", Output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    /// <summary>同步属性修改后的端口缓存；非法输入不吞异常，由属性面板显示校验信息。</summary>
    private void RefreshOutputValue()
    {
        Output.Data = ParseValue();
        UpdatePreview();
    }

    /// <summary>更新画布值预览，不在编辑属性时触发下游节点执行。</summary>
    private void UpdatePreview()
    {
        RuntimeText = Output.Data is null ? "null" : Convert.ToString(Output.Data, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>先联合校验保存的类型和值，再恢复其他属性，不依赖反射或 JSON 字段顺序。</summary>
    protected internal override void OnLoadNode(Dictionary<string, byte[]> values)
    {
        var valueType = values.TryGetValue(nameof(ValueType), out var typeBytes)
            ? Enum.Parse<WorkflowDataValueType>(Encoding.UTF8.GetString(typeBytes)) : _valueType;
        var text = values.TryGetValue(nameof(Value), out var valueBytes)
            ? Encoding.UTF8.GetString(valueBytes) : WorkflowDataValueConverter.GetDefaultText(valueType);
        var parsed = WorkflowDataValueConverter.Parse(text, valueType);
        var otherProperties = new Dictionary<string, byte[]>(values);
        otherProperties.Remove(nameof(ValueType));
        otherProperties.Remove(nameof(Value));
        base.OnLoadNode(otherProperties);
        Output.SetDataType(WorkflowDataValueConverter.GetClrType(valueType));
        _valueType = valueType;
        _value = text;
        Output.Data = parsed;
        UpdatePreview();
    }

    /// <summary>读取当前常量的强类型值。</summary>
    private object? ParseValue() => WorkflowDataValueConverter.Parse(_value, _valueType);
}
