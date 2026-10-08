using System.ComponentModel;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Data;

/// <summary>通过键读写进程级全局数据的节点，输入写入、输出读取。</summary>
[XTNode("数据", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "按键名读写当前进程共享数据；实际输入传值时写入，执行时读取。编辑与加载不写入全局数据，重启后数据不保留。")]
[DisplayName("全局数据")]
public sealed class GlobalDataNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private string _key = "workflow.value";
    private WorkflowDataValueType _valueType = WorkflowDataValueType.Object;

    /// <summary>创建全局数据节点并注册读写端口。</summary>
    public GlobalDataNode()
    {
        // SetNodeTypeTitle("全局数据");
        TitleColor = Color.FromRgb(65, 139, 218);
        Input = InputOptions.Add("写入", typeof(object), true);
        Output = OutputOptions.Add("读取", typeof(object), false);
        Input.DataTransfer += OnInputDataTransfer;
        RuntimeText = "未读取";
    }

    [XTNodeProperty("全局键", "相同键名的全局数据节点共享同一份数据，键名不区分大小写。")]
    public string Key
    {
        get => _key;
        set
        {
            VerifyAccess();
            var key = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("全局数据键不能为空。") : value.Trim();
            if (_key == key) return;
            _key = key;
            ClearCachedValue();
        }
    }

    [XTNodeProperty("数据类型", "限制读写端口的数据类型；Object 表示不限制具体 CLR 类型。")]
    public WorkflowDataValueType ValueType
    {
        get => _valueType;
        set
        {
            if (_valueType == value) return;
            VerifyAccess();
            var dataType = WorkflowDataValueConverter.GetClrType(value);
            // 同时检查两侧端口，避免只改完输入端才发现输出端仍有连接。
            if (Input.ConnectionCount > 0 || Output.ConnectionCount > 0)
                throw new InvalidOperationException("请先断开输入和输出连接，再修改数据类型。");
            Input.SetDataType(dataType);
            Output.SetDataType(dataType);
            _valueType = value;
            ClearCachedValue();
        }
    }

    public XTNodeOption Input { get; }
    public XTNodeOption Output { get; }

    /// <summary>校验键名和已有数据类型，防止执行时才发现节点配置无效。</summary>
    public EditorNodeReadinessResult CanExecute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            _ = NormalizeKey();
            if (!GlobalDataStore.TryGet(Key, out var value))
                return Input.ConnectionCount > 0 ? EditorNodeReadinessResult.Ready()
                    : EditorNodeReadinessResult.NotReady($"全局数据“{Key}”尚未写入。");
            _ = WorkflowDataValueConverter.ConvertValue(value, ValueType);
            return EditorNodeReadinessResult.Ready();
        }
        catch (Exception exception)
        {
            return EditorNodeReadinessResult.NotReady(exception.Message);
        }
    }

    /// <summary>读取全局键并广播当前值；未定义的键返回失败，与显式存储的 null 区分。</summary>
    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            RefreshOutputValue();
            Output.TransferData(Output.Data);
            return EditorNodeExecutionResult.Success($"已读取全局数据“{Key}”。", Output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            RuntimeText = "读取失败";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    /// <summary>接收上游输出时写入全局存储，并立即把最新值转发给下游。</summary>
    private void OnInputDataTransfer(object sender, XTNodeOptionEventArgs args)
    {
        // 建立或恢复连接也会产生通知，只有主动广播才代表实际写入，避免加载画布污染进程数据。
        if (args.Status != ConnectionStatus.Connected || !args.IsSponsor) return;
        var value = WorkflowDataValueConverter.ConvertValue(args.TargetOption.Data, ValueType);
        GlobalDataStore.Set(NormalizeKey(), value);
        Input.Data = value;
        Output.TransferData(value);
        RuntimeText = value is null ? "null" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>从全局存储刷新输出缓存，不改变全局数据。</summary>
    private void RefreshOutputValue()
    {
        if (!GlobalDataStore.TryGet(NormalizeKey(), out var value))
            throw new InvalidOperationException($"全局数据“{Key}”尚未写入。");
        Output.Data = WorkflowDataValueConverter.ConvertValue(value, ValueType);
        RuntimeText = Output.Data is null ? "null" : Convert.ToString(Output.Data, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>切换键或类型时清除旧缓存，只在实际执行时读取新键，编辑过程不访问全局数据。</summary>
    private void ClearCachedValue()
    {
        Input.Data = null;
        Output.Data = null;
        RuntimeText = "未读取";
    }

    /// <summary>校验当前全局键并返回去除首尾空格后的结果。</summary>
    private string NormalizeKey()
    {
        var key = (Key ?? string.Empty).Trim();
        if (key.Length == 0) throw new InvalidOperationException("全局数据键不能为空。");
        return key;
    }
}
