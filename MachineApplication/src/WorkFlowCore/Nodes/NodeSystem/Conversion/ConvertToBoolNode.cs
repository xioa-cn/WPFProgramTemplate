using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("转为布尔值")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "转为布尔值；使用固定文化，输入无效时返回节点错误。")]
public sealed class ConvertToBoolNode : ValueNode
{
    public ConvertToBoolNode() : base("转为布尔值", typeof(bool))
    {
        ValueJson = "true";

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        if (values[0] is null && "Boolean" != "String") throw new ArgumentException("null 不能转换为非空值类型。");
        return WorkFlowCore.Nodes.Data.WorkflowDataValueConverter.ConvertValue(values[0], WorkFlowCore.Nodes.Data.WorkflowDataValueType.Boolean);
    }
}
