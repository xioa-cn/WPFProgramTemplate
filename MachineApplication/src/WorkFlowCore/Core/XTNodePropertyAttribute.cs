using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;

namespace ST.Library.UI.NodeEditor;

/// <summary>标记可编辑和持久化的节点属性，允许自定义描述器扩展转换规则。</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public class XTNodePropertyAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public Type DescriptorType { get; set; } = typeof(XTNodePropertyDescriptor);
}

/// <summary>WPF 属性描述器，文本转换使用固定文化，不依赖当前界面语言。</summary>
public class XTNodePropertyDescriptor
{
    public XTNode Node { get; internal set; } = null!;
    public XTNodePropertyGrid? Control { get; internal set; }
    public PropertyInfo PropertyInfo { get; internal set; } = null!;
    public string Name { get; internal set; } = "";
    public string Description { get; internal set; } = "";
    public Rect Rectangle { get; internal set; }
    public Rect RectangleL { get; internal set; }
    public Rect RectangleR { get; internal set; }
    public bool IsReadOnly => PropertyInfo.SetMethod?.IsPublic != true || Control?.ReadOnlyModel == true;

    /// <summary>只枚举显式标记的公共实例属性，不调用索引器。</summary>
    internal static IEnumerable<XTNodePropertyDescriptor> Create(XTNode node, XTNodePropertyGrid? control = null)
    {
        foreach (var property in node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<XTNodePropertyAttribute>();
            if (attribute is null || property.GetMethod is null || property.GetIndexParameters().Length != 0) continue;
            if (!typeof(XTNodePropertyDescriptor).IsAssignableFrom(attribute.DescriptorType) || attribute.DescriptorType.IsAbstract)
                throw new InvalidOperationException("属性描述器必须继承 XTNodePropertyDescriptor。");
            var descriptor = (XTNodePropertyDescriptor)Activator.CreateInstance(attribute.DescriptorType)!;
            descriptor.Node = node;
            descriptor.Control = control;
            descriptor.PropertyInfo = property;
            descriptor.Name = attribute.Name;
            descriptor.Description = attribute.Description;
            yield return descriptor;
        }
    }
    protected internal virtual object? GetValueFromString(string text)
    {
        var nullable = Nullable.GetUnderlyingType(PropertyInfo.PropertyType);
        if (nullable is not null && string.IsNullOrWhiteSpace(text)) return null;
        var type = nullable ?? PropertyInfo.PropertyType;
        if (type == typeof(string)) return text;
        return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
    }
    protected internal virtual string GetStringFromValue()
    {
        var value = GetValue(null);
        if (value is null) return "";
        return TypeDescriptor.GetConverter(value.GetType()).ConvertToInvariantString(value) ?? "";
    }
    protected internal virtual string GetSelectItemText(object value) =>
        value.GetType().GetField(value.ToString() ?? "")?.GetCustomAttribute<DescriptionAttribute>()?.Description
        ?? Convert.ToString(value, CultureInfo.CurrentUICulture) ?? "";

    internal XTNodePropertySelectItem[] GetSelectItems(IEnumerable<object> values) =>
        values.Select(value => new XTNodePropertySelectItem(value, GetSelectItemText(value))).ToArray();
    protected internal virtual byte[] GetBytesFromValue() => Encoding.UTF8.GetBytes(GetStringFromValue());
    protected internal virtual object? GetValueFromBytes(byte[] bytes) => GetValueFromString(Encoding.UTF8.GetString(bytes));
    protected internal virtual object? GetValue(object[]? index) => PropertyInfo.GetValue(Node, index);
    protected internal virtual void SetValue(object? value) => SetValue(value, null);
    protected internal virtual void SetValue(string value) => SetValue(GetValueFromString(value), null);
    protected internal virtual void SetValue(byte[] value) => SetValue(GetValueFromBytes(value), null);
    protected internal virtual void SetValue(object? value, object[]? index)
    {
        if (IsReadOnly) throw new InvalidOperationException("该属性只读。");
        PropertyInfo.SetValue(Node, value, index);
        Node.BuildSize(true, true, true);
    }
    protected internal virtual void SetValue(string value, object[]? index) => SetValue(GetValueFromString(value), index);
    protected internal virtual void SetValue(byte[] value, object[]? index) => SetValue(GetValueFromBytes(value), index);
    protected internal virtual void OnSetValueError(Exception exception) => Control?.SetErrorMessage(exception.GetBaseException().Message);
    public void Invalidate() { Node.Invalidate(); Control?.RefreshProperties(); }
}

internal sealed record XTNodePropertySelectItem(object Value, string Text);
