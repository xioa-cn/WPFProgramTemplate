namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算非负数的平方根；负数返回失败，输出 Double。")]
[DisplayName("平方根")]
public sealed class SqrtNode : UnaryMathNode
{
    public SqrtNode() : base("平方根")
    {
        SetOutputDescription("平方根 = √数值", "输出平方后等于输入的非负数。例如输入 9，输出 3；输入 0，输出 0；负数输入失败。");
    }

    protected override void ValidateValues(double[] values)
    {
        if (values[0] < 0) throw new ArgumentOutOfRangeException(nameof(Value), "平方根的输入不能为负数。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Sqrt(values[0]);
}
