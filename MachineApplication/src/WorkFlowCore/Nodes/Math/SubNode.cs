namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算左值减去右值，输出 Double。")]
[DisplayName("减法")]
public sealed class SubNode : BinaryMathNode
{
    public SubNode() : base("减法")
    {
        SetOutputDescription("差 = 左值 − 右值", "用左值减去右值，输入顺序影响结果。例如左值 3、右值 2，输出 1。");
    }

    protected override double Calculate(double[] values) => values[0] - values[1];
}
