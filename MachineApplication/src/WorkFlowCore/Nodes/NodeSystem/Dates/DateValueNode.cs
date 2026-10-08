using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class DateValueNode : SystemNode
{
    protected DateValueNode(string title, Type outputType) : base(title, outputType)
    {
        DateInput = AddInput("日期时间", () => DateText);
    }

    [XTNodeProperty("日期时间", "未连接时使用 ISO 日期文本；连接可接收 DateTime。")]
    public string DateText { get; set; } = "2000-01-01T00:00:00";
    public XTNodeOption DateInput { get; }
    protected override void ValidateValues(object?[] values) => _ = SystemValues.Date(values[0]);
}

public enum DateAddUnit { Years, Months, Days, Hours, Minutes, Seconds, Milliseconds }
