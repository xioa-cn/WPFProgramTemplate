namespace WorkFlowCore.Nodes.Math;

using global::System.ComponentModel;
using ST.Library.UI.NodeEditor;

[XTNode("数学运算", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按指定小数位数舍入，默认中点远离零（四舍五入），输出 Double。")]
[DisplayName("舍入")]
public sealed class RoundNode : UnaryMathNode
{
    private int _digits;
    private MidpointRounding _roundingMode = MidpointRounding.AwayFromZero;

    public RoundNode() : base("舍入")
    {
        SetOutputDescription("按位数舍入后的数值", "按“小数位数”和“舍入模式”处理输入。默认保留 0 位、中点远离零：2.5 → 3，-2.5 → -3。选择 ToEven 时中点取偶数，例如 2.5 → 2。");
    }

    [XTNodeProperty("小数位数", "保留的小数位数，范围为 0 到 15，默认 0。")]
    public int Digits
    {
        get => _digits;
        set
        {
            if (value is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(value), "小数位数必须在 0 到 15 之间。");
            _digits = value;
        }
    }

    [XTNodeProperty("舍入模式", "AwayFromZero：中点远离零；ToEven：中点取偶数；其余模式为定向舍入。")]
    public MidpointRounding RoundingMode
    {
        get => _roundingMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), "无效的舍入模式。");
            _roundingMode = value;
        }
    }

    protected override double Calculate(double[] values) => global::System.Math.Round(values[0], Digits, RoundingMode);
}
