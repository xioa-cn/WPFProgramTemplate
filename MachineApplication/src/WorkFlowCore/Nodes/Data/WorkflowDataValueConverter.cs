using System.Globalization;

namespace WorkFlowCore.Nodes.Data;

/// <summary>统一负责数据节点的类型映射、文本解析和可读文本转换。</summary>
internal static class WorkflowDataValueConverter
{
    /// <summary>将界面类型枚举转换为端口实际使用的 CLR 类型。</summary>
    public static Type GetClrType(WorkflowDataValueType valueType) => valueType switch
    {
        WorkflowDataValueType.String => typeof(string),
        WorkflowDataValueType.Int32 => typeof(int),
        WorkflowDataValueType.Int64 => typeof(long),
        WorkflowDataValueType.Double => typeof(double),
        WorkflowDataValueType.Decimal => typeof(decimal),
        WorkflowDataValueType.Boolean => typeof(bool),
        WorkflowDataValueType.DateTime => typeof(DateTime),
        WorkflowDataValueType.Object => typeof(object),
        _ => throw new ArgumentOutOfRangeException(nameof(valueType), "不支持的数据类型。")
    };

    /// <summary>切换类型且旧文本无法转换时使用明确的默认值，避免节点陷入不可编辑状态。</summary>
    public static string GetDefaultText(WorkflowDataValueType valueType) => valueType switch
    {
        WorkflowDataValueType.String or WorkflowDataValueType.Object => string.Empty,
        WorkflowDataValueType.Boolean => "false",
        WorkflowDataValueType.DateTime => DateTime.MinValue.ToString("O", CultureInfo.InvariantCulture),
        WorkflowDataValueType.Int32 or WorkflowDataValueType.Int64 or WorkflowDataValueType.Double or WorkflowDataValueType.Decimal => "0",
        _ => throw new ArgumentOutOfRangeException(nameof(valueType), "不支持的数据类型。")
    };

    /// <summary>按固定文化解析节点文本；Object 类型保留原始字符串，便于通用传递。</summary>
    public static object? Parse(string? text, WorkflowDataValueType valueType)
    {
        var value = text ?? string.Empty;
        _ = GetClrType(valueType);
        if (valueType == WorkflowDataValueType.Object || valueType == WorkflowDataValueType.String)
            return value;
        if (valueType == WorkflowDataValueType.Boolean && bool.TryParse(value, out var boolean)) return boolean;
        if (valueType == WorkflowDataValueType.Int32 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
        if (valueType == WorkflowDataValueType.Int64 && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue)) return longValue;
        if (valueType == WorkflowDataValueType.Double && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue) && double.IsFinite(doubleValue)) return doubleValue;
        if (valueType == WorkflowDataValueType.Decimal && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue)) return decimalValue;
        if (valueType == WorkflowDataValueType.DateTime && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)) return dateTime;
        throw new FormatException($"“{value}”不是有效的 {valueType} 值。");
    }

    /// <summary>将全局存储中的值转换为目标类型，类型不匹配时抛出清晰的节点错误。</summary>
    public static object? ConvertValue(object? value, WorkflowDataValueType valueType)
    {
        var targetType = GetClrType(valueType);
        if (value is null) return null;
        if (targetType == typeof(object)) return value;
        try
        {
            var converted = targetType.IsInstanceOfType(value) ? value
                : value is string text ? Parse(text, valueType) : Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            if (converted is double number && !double.IsFinite(number)) throw new FormatException("不支持无穷大或非数字值。");
            return converted;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"全局数据无法转换为 {valueType}。", exception);
        }
    }
}
