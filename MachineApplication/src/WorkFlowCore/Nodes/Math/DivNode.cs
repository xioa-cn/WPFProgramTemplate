namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算左值除以右值，输出 Double；除数不能为零。")]
[DisplayName("除法")]
public sealed class DivNode : BinaryMathNode
{
    public DivNode() : base("除法", 1)
    {
        SetOutputDescription("商 = 左值 ÷ 右值", "左值是被除数，右值是除数，保留小数。例如 7 ÷ 2 输出 3.5；右值为 0 时失败。");
    }

    protected override void ValidateValues(double[] values)
    {
        if (values[1] == 0) throw new DivideByZeroException("除数不能为零。");
    }

    protected override double Calculate(double[] values) => values[0] / values[1];
}
