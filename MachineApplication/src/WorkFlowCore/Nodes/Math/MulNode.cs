namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算左值与右值之积，输出 Double。")]
[DisplayName("乘法")]
public sealed class MulNode : BinaryMathNode
{
    public MulNode() : base("乘法", 1)
    {
        SetOutputDescription("积 = 左值 × 右值", "将左右两个输入相乘。例如左值 3、右值 2，输出 6。");
    }

    protected override double Calculate(double[] values) => values[0] * values[1];
}
