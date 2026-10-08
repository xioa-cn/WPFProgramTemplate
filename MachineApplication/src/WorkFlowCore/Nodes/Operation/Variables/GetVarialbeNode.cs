using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("读取变量")]
[XTNode("变量操作", "xioa", "", "", "读取当前上下文或进程全局变量；缺失时可配置默认 JSON 值。运行值不随节点保存。")]
public sealed class GetVarialbeNode : VariableNode
{
    public GetVarialbeNode() : base("读取变量")
    {
        TriggerInput = AddInput("触发", () => null);
    }

    [XTNodeProperty("缺失时使用默认值", "关闭时变量不存在会执行失败；显式存储的 null 不是缺失。")]
    public bool UseDefaultWhenMissing { get; set; }

    [XTNodeProperty("默认值（JSON）", "仅变量不存在且启用默认值时使用。")]
    public string DefaultJson { get; set; } = "null";

    public XTNodeOption TriggerInput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        if (TryRead(context, out var value)) return value;
        if (UseDefaultWhenMissing) return OperationValues.Parse(DefaultJson);
        if (preview) return null;
        throw new KeyNotFoundException($"变量“{VariableName.Trim()}”不存在。");
    }
}
