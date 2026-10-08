namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "向负无穷方向取整，例如 -1.2 得到 -2；输出 Double。")]
[DisplayName("向下取整")]
public sealed class FloorNode : UnaryMathNode
{
    public FloorNode() : base("向下取整")
    {
        SetOutputDescription("向下取整 = floor(数值)", "取不大于输入的最大整数（向负无穷方向），不是直接去掉小数。例如 1.8 → 1，-1.2 → -2。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Floor(values[0]);
}
