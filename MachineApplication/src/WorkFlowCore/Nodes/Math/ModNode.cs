namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "计算左值除以右值的余数，符号与左值一致，支持小数；除数不能为零。")]
[DisplayName("取余")]
public sealed class ModNode : BinaryMathNode
{
    public ModNode() : base("取余", 1)
    {
        SetOutputDescription("余数 = 左值 % 右值", "输出左值除以右值后的余数，符号与左值一致。例如 7 % 2 输出 1，-7 % 2 输出 -1；右值为 0 时失败。");
    }

    protected override void ValidateValues(double[] values)
    {
        if (values[1] == 0) throw new DivideByZeroException("取余的除数不能为零。");
    }

    protected override double Calculate(double[] values) => values[0] % values[1];
}
