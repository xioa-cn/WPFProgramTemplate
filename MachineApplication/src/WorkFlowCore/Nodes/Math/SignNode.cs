namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "获取数值符号：负数为 -1，零为 0，正数为 1；输出 Double。")]
[DisplayName("符号")]
public sealed class SignNode : UnaryMathNode
{
    public SignNode() : base("符号")
    {
        SetOutputDescription("符号 = -1 / 0 / 1", "输入小于 0 输出 -1，等于 0 输出 0，大于 0 输出 1。例如输入 -2.5，输出 -1。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Sign(values[0]);
}
