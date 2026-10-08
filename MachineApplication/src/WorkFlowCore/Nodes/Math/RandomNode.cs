namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "每次执行生成包含下限、不包含上限的 Double 随机数；上下限相同时输出该值。")]
[DisplayName("随机数")]
public sealed class RandomNode : MathNode
{
    public RandomNode() : base("随机数")
    {
        MinimumInput = AddInput("下限", () => Minimum);
        MaximumInput = AddInput("上限", () => Maximum);
        SetOutputDescription("随机数 ∈ [下限, 上限)", "每次执行在下限与上限之间随机取一个小数，包含下限、不包含上限。例如范围 [0, 1) 中可能输出 0.37。上下限相同时输出该值，下限大于上限时失败。");
    }

    [XTNodeProperty("下限", "下限输入未连接且未赋值时使用，结果包含下限。")]
    public double Minimum { get; set; }

    [XTNodeProperty("上限", "上限输入未连接且未赋值时使用；除上下限相同外，结果不包含上限。")]
    public double Maximum { get; set; } = 1;

    public XTNodeOption MinimumInput { get; }
    public XTNodeOption MaximumInput { get; }

    protected override void ValidateValues(double[] values)
    {
        if (values[0] > values[1]) throw new ArgumentException("下限不能大于上限。");
    }

    protected override double Calculate(double[] values)
    {
        var minimum = values[0];
        var maximum = values[1];
        if (minimum == maximum) return minimum;
        var fraction = global::System.Random.Shared.NextDouble();
        var result = minimum * (1 - fraction) + maximum * fraction;
        return global::System.Math.Clamp(result, minimum, global::System.Math.BitDecrement(maximum));
    }
}
