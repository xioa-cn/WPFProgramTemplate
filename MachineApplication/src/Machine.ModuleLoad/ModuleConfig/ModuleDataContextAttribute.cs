namespace Machine.ModuleLoad.ModuleConfig;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ModuleDataContextAttribute<T>(string viewModelModuleName) : Attribute
{
    public string ViewModelModuleName { get; set; } = viewModelModuleName;
    public Type ViewModelType { get; set; } = typeof(T);
}
