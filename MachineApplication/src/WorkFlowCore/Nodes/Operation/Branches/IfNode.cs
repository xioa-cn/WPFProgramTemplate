using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("条件分支")]
[XTNode("流程控制", "xioa", "", "", "条件为 true 仅激活真分支，为 false 仅激活假分支；输出流程信号。")]
public sealed class IfNode : OperationNode
{
    public IfNode() : base("条件分支", typeof(object))
    {
        ConditionInput = AddInput("条件", () => Condition);
        Output.Text = "真";
        FalseOutput = OutputOptions.Add("假", typeof(object), false);
    }

    [XTNodeProperty("条件", "未连接时使用的 Boolean 条件。")]
    public bool Condition { get; set; }

    public XTNodeOption ConditionInput { get; }
    public XTNodeOption TrueOutput => Output;
    public XTNodeOption FalseOutput { get; }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) => OperationValues.Boolean(values[0]);

    protected override XTNodeOption[] Publish(object? value, EditorExecutionContext context)
    {
        var selected = (bool)value! ? TrueOutput : FalseOutput;
        selected.TransferData(new EditorFlowSignal(context.ExecutionId));
        return [selected];
    }
}
