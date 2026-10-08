namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "将数值限制在包含上下限的区间内，下限不能大于上限。")]
[DisplayName("限幅")]
public sealed class ClampNode : MathNode
{
    public ClampNode() : base("限幅")
    {
        Input = AddInput("数值", () => Value);
        MinimumInput = AddInput("下限", () => Minimum);
        MaximumInput = AddInput("上限", () => Maximum);
        SetOutputDescription("限幅后的数值", "结果 = min(max(数值, 下限), 上限)。小于下限时输出下限，大于上限时输出上限，否则原样输出。例如范围 [0, 1] 中输入 3，输出 1。下限大于上限时失败。");
    }

    [XTNodeProperty("数值", "输入未连接且未赋值时使用的默认数值。")]
    public double Value { get; set; }

    [XTNodeProperty("下限", "下限输入未连接且未赋值时使用，必须小于或等于上限。")]
    public double Minimum { get; set; }

    [XTNodeProperty("上限", "上限输入未连接且未赋值时使用，必须大于或等于下限。")]
    public double Maximum { get; set; } = 1;

    public XTNodeOption Input { get; }
    public XTNodeOption MinimumInput { get; }
    public XTNodeOption MaximumInput { get; }

    protected override void ValidateValues(double[] values)
    {
        if (values[1] > values[2]) throw new ArgumentException("下限不能大于上限。");
    }

    protected override double Calculate(double[] values) => global::System.Math.Clamp(values[0], values[1], values[2]);
}
