using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Data;

namespace WorkFlowCore.Nodes.Operation;

public abstract class VariableNode : OperationNode
{
    private const string ContextPrefix = "WorkFlowCore.Variable:";
    private sealed record StoredValue(object? Value);

    protected VariableNode(string title) : base(title, typeof(object)) { }

    [XTNodeProperty("变量名", "去除首尾空白，不区分大小写；名称不能为空。")]
    public string VariableName { get; set; } = "value";

    [XTNodeProperty("作用域", "Context 当前执行上下文内共享；Global 与全局数据节点共享，进程退出后不保留。")]
    public VariableScope Scope { get; set; } = VariableScope.Context;

    protected override void ValidateSettings()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(VariableName);
        if (!Enum.IsDefined(Scope)) throw new ArgumentOutOfRangeException(nameof(Scope));
    }

    protected void Store(EditorExecutionContext context, object? value)
    {
        if (Scope == VariableScope.Global) GlobalDataStore.Set(VariableName, value);
        else context.Items[ContextPrefix + VariableName.Trim()] = new StoredValue(value);
    }

    protected bool TryRead(EditorExecutionContext context, out object? value)
    {
        if (Scope == VariableScope.Global) return GlobalDataStore.TryGet(VariableName, out value);
        if (context.Items.TryGetValue(ContextPrefix + VariableName.Trim(), out var stored) && stored is StoredValue entry)
        {
            value = entry.Value;
            return true;
        }
        value = null;
        return false;
    }
}

public enum VariableScope { Context, Global }
