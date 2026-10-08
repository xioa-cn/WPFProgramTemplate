using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("反序列化 JSON")]
[XTNode("JSON 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "反序列化 JSON；使用固定文化，输入无效时返回节点错误。")]
public sealed class JsonDeserializeNode : TextValueNode
{
    public JsonDeserializeNode() : base("反序列化 JSON", typeof(object))
    {
        Text = "{}";
    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return SystemValues.JsonValue(SystemValues.Text(values[0]));
    }
}
