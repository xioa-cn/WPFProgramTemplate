using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("日期时间加减")]
[XTNode("日期时间", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "日期时间加减；使用固定文化，输入无效时返回节点错误。")]
public sealed class DateTimeAddNode : DateValueNode
{
    public DateTimeAddNode() : base("日期时间加减", typeof(DateTime))
    {
        AmountInput = AddInput("增量", () => Amount);
    }

    [XTNodeProperty("增量", "可为负数；年和月仅支持整数。")]
    public double Amount { get; set; } = 1;
    [XTNodeProperty("单位", "Years、Months、Days、Hours、Minutes、Seconds 或 Milliseconds。")]
    public DateAddUnit Unit { get; set; } = DateAddUnit.Days;
    public XTNodeOption AmountInput { get; }
    protected override void ValidateSettings()
    {
        if (!Enum.IsDefined(Unit)) throw new ArgumentOutOfRangeException(nameof(Unit));
    }
    protected override void ValidateValues(object?[] values)
    {
        base.ValidateValues(values);
        if (!WorkFlowCore.Nodes.Operation.OperationValues.IsNumber(values[1])) throw new ArgumentException("增量必须为数字。");
        var amount = Convert.ToDouble(values[1], CultureInfo.InvariantCulture);
        if (!double.IsFinite(amount)) throw new ArgumentException("增量必须为有限数字。");
        if (Unit is DateAddUnit.Years or DateAddUnit.Months && (amount != System.Math.Truncate(amount) || amount > int.MaxValue || amount < int.MinValue))
            throw new ArgumentException("年和月增量必须为 Int32 范围内整数。");
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var date = SystemValues.Date(values[0]);
        var amount = Convert.ToDouble(values[1], CultureInfo.InvariantCulture);
        return Unit switch
        {
            DateAddUnit.Years => date.AddYears((int)amount),
            DateAddUnit.Months => date.AddMonths((int)amount),
            DateAddUnit.Days => date.AddDays(amount),
            DateAddUnit.Hours => date.AddHours(amount),
            DateAddUnit.Minutes => date.AddMinutes(amount),
            DateAddUnit.Seconds => date.AddSeconds(amount),
            _ => date.AddMilliseconds(amount)
        };
    }
}
