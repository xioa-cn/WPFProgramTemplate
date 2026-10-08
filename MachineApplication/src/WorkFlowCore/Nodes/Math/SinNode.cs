namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算正弦，输入单位为弧度（π 弧度 = 180°），输出 Double。")]
[DisplayName("正弦")]
public sealed class SinNode : UnaryMathNode
{
    public SinNode() : base("正弦", "弧度")
    {
        SetOutputDescription("正弦 = sin(弧度)", "计算输入角度的正弦，输入单位为弧度而非度，输出范围 [-1, 1]。例如输入 π/2（约 1.5708，即 90°），输出约 1。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Sin(values[0]);
}
