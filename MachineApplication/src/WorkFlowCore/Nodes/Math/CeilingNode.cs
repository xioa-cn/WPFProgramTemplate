namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "向正无穷方向取整，例如 1.2 得到 2，-1.8 得到 -1；输出 Double。")]
[DisplayName("向上取整")]
public sealed class CeilingNode : UnaryMathNode
{
    public CeilingNode() : base("向上取整")
    {
        SetOutputDescription("向上取整 = ceiling(数值)", "取不小于输入的最小整数（向正无穷方向），不是四舍五入。例如 1.2 → 2，-1.8 → -1；整数和零保持不变。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Ceiling(values[0]);
}
