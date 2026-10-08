using System.ComponentModel;
using System.Globalization;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Data;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("类型转换")]
[XTNode("类型操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "按固定文化转换基础类型；null 仅可转换为 Object 或 String，溢出和无效转换会失败。")]
public sealed class TypeConverterNode : UnaryValueNode
{
    public TypeConverterNode() : base("类型转换", typeof(object)) { ValueJson = "\"123\""; }

    [XTNodeProperty("目标类型", "支持 String、Boolean、Int32、Int64、Double、Decimal、DateTime 和 Object。")]
    public WorkflowDataValueType TargetType { get; set; } = WorkflowDataValueType.Int32;

    protected override void ValidateSettings() => _ = WorkflowDataValueConverter.GetClrType(TargetType);

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview)
    {
        var value = values[0];
        if (value is null && TargetType is not (WorkflowDataValueType.Object or WorkflowDataValueType.String))
            throw new InvalidOperationException("null 不能转换为非空值类型。");
        if (TargetType == WorkflowDataValueType.String && value is Type type) return type.FullName ?? type.Name;
        if (TargetType == WorkflowDataValueType.String && value is not null)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        return WorkflowDataValueConverter.ConvertValue(value, TargetType);
    }
}
