using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("字节转数值")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "从字节数组指定位置解码基础类型，字节不足时报错。")]
public sealed class BitConverterFromBytesNode : BytesNode
{
    public BitConverterFromBytesNode() : base("字节转数值", typeof(object)) { BytesJson = "[0,0,0,0]"; }
    [XTNodeProperty("数据类型", "解码的基础类型。")]
    public BinaryValueKind ValueKind { get; set; } = BinaryValueKind.Int32;
    [XTNodeProperty("字节序", "LittleEndian 小端，BigEndian 大端。")]
    public ByteOrder Order { get; set; } = ByteOrder.LittleEndian;
    [XTNodeProperty("起始索引", "从 0 开始的字节偏移。")]
    public int Offset { get; set; }
    protected override void ValidateSettings()
    {
        _ = BinaryValues.Size(ValueKind);
        if (!Enum.IsDefined(Order) || Offset < 0) throw new ArgumentException("字节序或偏移无效。");
    }
    protected override void ValidateValues(object?[] values) => _ = BinaryValues.Decode(SystemValues.Bytes(values[0]), ValueKind, Order, Offset);
    protected override object? Run(object?[] values, CancellationToken cancellationToken) => BinaryValues.Decode(SystemValues.Bytes(values[0]), ValueKind, Order, Offset);
}
