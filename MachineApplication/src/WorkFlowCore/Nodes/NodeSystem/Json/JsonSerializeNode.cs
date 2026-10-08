using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("序列化 JSON")]
[XTNode("JSON 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "序列化 JSON；使用固定文化，输入无效时返回节点错误。")]
public sealed class JsonSerializeNode : ValueNode
{
    public JsonSerializeNode() : base("序列化 JSON", typeof(string))
    {

    }

    [XTNodeProperty("缩进格式", "启用后输出便于阅读的缩进 JSON。")]
    public bool WriteIndented { get; set; }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return JsonSerializer.Serialize(values[0], new JsonSerializerOptions { WriteIndented = WriteIndented });
    }
}
