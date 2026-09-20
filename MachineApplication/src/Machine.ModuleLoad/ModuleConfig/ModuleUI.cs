using Machine.ModuleLoad.Logger;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Machine.ModuleLoad.ModuleConfig;

public static class ModuleUI
{
    /// <summary>根据特性从主容器或模块子容器解析 ViewModel，并设置依赖对象的 DataContext。</summary>
    /// <param name="element">待组装的 WPF 依赖对象。</param>
    /// <returns>已完成组装的原依赖对象。</returns>
    public static DependencyObject AssemblyUI(this DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var attribute = element.GetType().GetCustomAttributes(true)
            .FirstOrDefault(x => x.GetType().Name.StartsWith("ModuleDataContextAttribute", StringComparison.Ordinal));
        if (attribute is null) return element;

        var viewModelType = (Type?)attribute.GetType().GetProperty("ViewModelType")?.GetValue(attribute)
            ?? throw new InvalidOperationException("ModuleDataContextAttribute 未指定 ViewModelType。");
        var moduleName = (string?)attribute.GetType().GetProperty("ViewModelModuleName")?.GetValue(attribute);
        var provider = string.IsNullOrWhiteSpace(moduleName)
            ? MainProvider.ServiceProvider
            : ModuleProvider.GetModuleProvider(moduleName);
        if (provider is null) throw new InvalidOperationException("主服务容器尚未初始化。");

        var viewModel = provider.GetRequiredService(viewModelType);
        switch (element)
        {
            case FrameworkElement frameworkElement:
                frameworkElement.DataContext = viewModel;
                break;
            case FrameworkContentElement frameworkContentElement:
                frameworkContentElement.DataContext = viewModel;
                break;
            default:
                throw new InvalidOperationException($"类型 {element.GetType().FullName} 不支持 DataContext。");
        }

        GlobalLogger.DebuggerLogger?.Debug($"Assigned DataContext '{viewModelType.Name}' to '{element.GetType().Name}'.");
        return element;
    }
}
