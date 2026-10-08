using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("读取 JSON 属性")]
[XTNode("JSON 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "读取 JSON 属性；使用固定文化，输入无效时返回节点错误。")]
public sealed class JsonGetPropertyNode : TextValueNode
{
    public JsonGetPropertyNode() : base("读取 JSON 属性", typeof(object))
    {
        Text = "{\"value\":1}";
    }

    [XTNodeProperty("属性名", "精确匹配顶层属性名称，区分大小写，不解释点号路径。")]
    public string PropertyName { get; set; } = "value";
    protected override void ValidateValues(object?[] values)
    {
        if (values[0] is null) throw new ArgumentException("JSON 输入不能为 null。");
    }
    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var json = values[0] is string text ? text : JsonSerializer.Serialize(values[0]);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(PropertyName, out var property))
            throw new KeyNotFoundException($"JSON 对象不存在属性：{PropertyName}。");
        return SystemValues.JsonValue(property.GetRawText());
    }
}
