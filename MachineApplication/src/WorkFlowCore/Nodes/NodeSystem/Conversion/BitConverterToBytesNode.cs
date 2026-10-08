using System.ComponentModel;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("数值转字节")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按指定基础类型和字节序编码，不依赖机器字节序。")]
public sealed class BitConverterToBytesNode : ValueNode
{
    public BitConverterToBytesNode() : base("数值转字节", typeof(byte[])) { ValueJson = "0"; }
    [XTNodeProperty("数据类型", "编码后的字节长度由类型决定。")]
    public BinaryValueKind ValueKind { get; set; } = BinaryValueKind.Int32;
    [XTNodeProperty("字节序", "LittleEndian 小端，BigEndian 大端。")]
    public ByteOrder Order { get; set; } = ByteOrder.LittleEndian;
    protected override void ValidateSettings()
    {
        _ = BinaryValues.Size(ValueKind);
        if (!Enum.IsDefined(Order)) throw new ArgumentOutOfRangeException(nameof(Order));
    }
    protected override void ValidateValues(object?[] values) => _ = BinaryValues.Encode(values[0], ValueKind, Order);
    protected override object? Run(object?[] values, CancellationToken cancellationToken) => BinaryValues.Encode(values[0], ValueKind, Order);
}
