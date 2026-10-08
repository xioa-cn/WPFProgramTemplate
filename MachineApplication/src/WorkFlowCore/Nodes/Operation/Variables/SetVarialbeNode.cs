using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("写入变量")]
[XTNode("变量操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "仅执行时写入变量并输出原值和完成信号；编辑、加载及就绪检查不会写变量。")]
public sealed class SetVarialbeNode : VariableNode
{
    public SetVarialbeNode() : base("写入变量")
    {
        ValueInput = AddInput("值", () => OperationValues.Parse(ValueJson));
        Completed = OutputOptions.Add("完成", typeof(object), false);
    }

    [XTNodeProperty("值（JSON）", "未连接时使用，支持 null；例如 123 或带双引号的文本。")]
    public string ValueJson { get; set; } = "null";

    public XTNodeOption ValueInput { get; }
    public XTNodeOption Completed { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => values[0];

    protected override XTNodeOption[] Publish(object? value, EditorExecutionContext context)
    {
        Store(context, value);
        Output.TransferData(value);
        Completed.TransferData(new EditorFlowSignal(context.ExecutionId));
        return [Output, Completed];
    }
}
