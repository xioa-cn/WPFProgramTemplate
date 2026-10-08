namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算余弦，输入单位为弧度（π 弧度 = 180°），输出 Double。")]
[DisplayName("余弦")]
public sealed class CosNode : UnaryMathNode
{
    public CosNode() : base("余弦", "弧度")
    {
        SetOutputDescription("余弦 = cos(弧度)", "计算输入角度的余弦，输入单位为弧度而非度，输出范围 [-1, 1]。例如输入 0，输出 1；输入 π（约 3.1416，即 180°），输出约 -1。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Cos(values[0]);
}
