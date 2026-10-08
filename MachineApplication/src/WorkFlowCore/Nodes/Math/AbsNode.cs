using System.ComponentModel;

namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算输入数值的绝对值，输出 Double。")]
[DisplayName("绝对值")]
public sealed class AbsNode : MathNode
{
    public AbsNode() : base("绝对值")
    {
        Input = AddInput("数值", () => Value);
        SetOutputDescription("绝对值 = |数值|", "去掉输入的负号，非负数保持不变。例如输入 -3，输出 3。");
    }

    [XTNodeProperty("数值", "输入未连接且未赋值时使用的默认数值。")]
    public double Value { get; set; }

    public XTNodeOption Input { get; }

    protected override double Calculate(double[] values) => global::System.Math.Abs(values[0]);
}
