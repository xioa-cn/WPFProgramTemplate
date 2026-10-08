using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("转为整数")]
[XTNode("数据转换", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "转为整数；使用固定文化，输入无效时返回节点错误。")]
public sealed class ConvertToInt32Node : ValueNode
{
    public ConvertToInt32Node() : base("转为整数", typeof(int))
    {
        ValueJson = "0";

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        if (values[0] is null && "Int32" != "String") throw new ArgumentException("null 不能转换为非空值类型。");
        return WorkFlowCore.Nodes.Data.WorkflowDataValueConverter.ConvertValue(values[0], WorkFlowCore.Nodes.Data.WorkflowDataValueType.Int32);
    }
}
