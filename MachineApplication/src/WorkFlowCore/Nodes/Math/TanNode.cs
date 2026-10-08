namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算正切，输入为弧度；接近 π/2 + kπ 时结果可能很大，遵循 Double 浮点运算。")]
[DisplayName("正切")]
public sealed class TanNode : UnaryMathNode
{
    public TanNode() : base("正切", "弧度")
    {
        SetOutputDescription("正切 = tan(弧度)", "计算输入角度的正切，输入单位为弧度而非度。例如输入 π/4（约 0.7854，即 45°），输出约 1。接近 π/2 + kπ 时结果可能很大，按 Double 浮点计算。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Tan(values[0]);
}
