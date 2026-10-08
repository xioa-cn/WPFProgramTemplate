using System.Globalization;

namespace WorkFlowCore.Nodes.NodeSystem;

public enum BinaryValueKind { Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Char }
public enum ByteOrder { LittleEndian, BigEndian }

internal static class BinaryValues
{
    public static int Size(BinaryValueKind kind) => kind switch
    {
        BinaryValueKind.Boolean => 1,
        BinaryValueKind.Int16 or BinaryValueKind.UInt16 or BinaryValueKind.Char => 2,
        BinaryValueKind.Int32 or BinaryValueKind.UInt32 or BinaryValueKind.Single => 4,
        BinaryValueKind.Int64 or BinaryValueKind.UInt64 or BinaryValueKind.Double => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static byte[] Encode(object? value, BinaryValueKind kind, ByteOrder order)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.InvariantCulture;
        var bytes = kind switch
        {
            BinaryValueKind.Boolean => BitConverter.GetBytes(Convert.ToBoolean(value, culture)),
            BinaryValueKind.Int16 => BitConverter.GetBytes(Convert.ToInt16(value, culture)),
            BinaryValueKind.UInt16 => BitConverter.GetBytes(Convert.ToUInt16(value, culture)),
            BinaryValueKind.Int32 => BitConverter.GetBytes(Convert.ToInt32(value, culture)),
            BinaryValueKind.UInt32 => BitConverter.GetBytes(Convert.ToUInt32(value, culture)),
            BinaryValueKind.Int64 => BitConverter.GetBytes(Convert.ToInt64(value, culture)),
            BinaryValueKind.UInt64 => BitConverter.GetBytes(Convert.ToUInt64(value, culture)),
            BinaryValueKind.Single => BitConverter.GetBytes(Convert.ToSingle(value, culture)),
            BinaryValueKind.Double => BitConverter.GetBytes(Convert.ToDouble(value, culture)),
            BinaryValueKind.Char => BitConverter.GetBytes(Convert.ToChar(value, culture)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (BitConverter.IsLittleEndian != (order == ByteOrder.LittleEndian)) Array.Reverse(bytes);
        return bytes;
    }

    public static object Decode(byte[] input, BinaryValueKind kind, ByteOrder order, int offset)
    {
        var size = Size(kind);
        if (offset < 0 || offset > input.Length - size) throw new ArgumentOutOfRangeException(nameof(offset), "字节数量不足或起始索引越界。");
        var bytes = input.AsSpan(offset, size).ToArray();
        if (BitConverter.IsLittleEndian != (order == ByteOrder.LittleEndian)) Array.Reverse(bytes);
        return kind switch
        {
            BinaryValueKind.Boolean => BitConverter.ToBoolean(bytes),
            BinaryValueKind.Int16 => BitConverter.ToInt16(bytes),
            BinaryValueKind.UInt16 => BitConverter.ToUInt16(bytes),
            BinaryValueKind.Int32 => BitConverter.ToInt32(bytes),
            BinaryValueKind.UInt32 => BitConverter.ToUInt32(bytes),
            BinaryValueKind.Int64 => BitConverter.ToInt64(bytes),
            BinaryValueKind.UInt64 => BitConverter.ToUInt64(bytes),
            BinaryValueKind.Single => BitConverter.ToSingle(bytes),
            BinaryValueKind.Double => BitConverter.ToDouble(bytes),
            BinaryValueKind.Char => BitConverter.ToChar(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
