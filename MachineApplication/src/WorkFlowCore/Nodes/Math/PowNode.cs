namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算底数的指数次幂，输出 Double；默认指数为 2。")]
[DisplayName("幂运算")]
public sealed class PowNode : BinaryMathNode
{
    public PowNode() : base("幂运算", 2)
    {
        SetOptionText(LeftInput, "底数");
        SetOptionText(RightInput, "指数");
        LeftInput.Description = "底数；未连接且未赋值时使用左值属性。";
        RightInput.Description = "指数；未连接且未赋值时使用右值属性，默认为 2（平方）。";
        SetOutputDescription("幂 = 底数 ^ 指数", "左值为底数，右值为指数。例如 2 的 3 次幂输出 8；指数为 0 时输出 1（包括 0 的 0 次幂）。负底数只支持整数指数，0 不支持负指数；结果溢出时失败。");
    }

    protected override void ValidateValues(double[] values)
    {
        if (values[0] == 0 && values[1] < 0)
            throw new DivideByZeroException("底数为 0 时，指数不能为负数。");
        if (values[0] < 0 && values[1] != global::System.Math.Truncate(values[1]))
            throw new ArgumentOutOfRangeException(nameof(RightValue), "负底数的指数必须为整数，当前节点不支持复数结果。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Pow(values[0], values[1]);
}
