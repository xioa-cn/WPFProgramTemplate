using System.ComponentModel;
using System.Windows;
using System.Windows.Navigation;

namespace Machine.ModuleLoad.Utils;

/// <summary>只在 WPF 设计模式下为页面合并样式，不改变运行时主题。</summary>
public static class ViewDesignInclude
{
    // Rider does not consistently set WPF design metadata on newly created objects.
    public static bool IsDesignMode(DependencyObject element) =>
        DesignerProperties.GetIsInDesignMode(element) ||
        AppDomain.CurrentDomain.FriendlyName.StartsWith(
            "JetBrains.ReSharper.Features.Xaml.Previewer", StringComparison.OrdinalIgnoreCase) ||
        (Application.Current?.GetType().Assembly.GetName().Name?.Contains(
            "Xaml.Previewer", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>由 XAML 触发设计时加载，不依赖根页面的后台构造函数。</summary>
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source", typeof(string), typeof(ViewDesignInclude),
            new PropertyMetadata(null, OnSourceChanged));

    public static void SetSource(DependencyObject element, string? value) =>
        element.SetValue(SourceProperty, value);

    public static string? GetSource(DependencyObject element) =>
        (string?)element.GetValue(SourceProperty);

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement view && args.NewValue is string source && !string.IsNullOrWhiteSpace(source))
        {
            if (source.Trim() == ".")
                view.IncludeDesignStyles();
            else
                view.IncludeDesignStyles(new Uri(source, UriKind.RelativeOrAbsolute));
        }
    }


    /// <summary>
    /// 加载当前页面程序集中的 Design/SharedStyles.xaml。
    /// 建议在 InitializeComponent 之前调用，以便解析页面内的 StaticResource。
    /// </summary>
    public static T IncludeDesignStyles<T>(this T view) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!IsDesignMode(view))
            return view;

        // A designer may substitute the root with a proxy. Prefer the XAML URI.
        var baseUri = BaseUriHelper.GetBaseUri(view);
        var path = baseUri?.OriginalString;
        var componentEnd = path?.IndexOf(";component/", StringComparison.OrdinalIgnoreCase) ?? -1;
        if (componentEnd >= 0)
        {
            var componentRoot = path![..(componentEnd + ";component/".Length)];
            return view.IncludeDesignStyles(new Uri(
                componentRoot + "Design/SharedStyles.xaml", UriKind.RelativeOrAbsolute));
        }

        var assembly = view.GetType().Assembly;
        if (assembly == typeof(FrameworkElement).Assembly ||
            (assembly.GetName().Name?.Contains("Xaml.Previewer", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            // Rider can create a plain UserControl without a component BaseUri.
            // Inspect resource indexes only; do not instantiate arbitrary views.
            var candidates = new List<string>();
            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaded.IsDynamic) continue;
                var name = loaded.GetName().Name;
                using var stream = loaded.GetManifestResourceStream(name + ".g.resources");
                if (stream is null) continue;
                using var reader = new System.Resources.ResourceReader(stream);
                var entries = reader.GetEnumerator();
                while (entries.MoveNext())
                {
                    if (string.Equals(entries.Key as string, "design/sharedstyles.baml", StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(name!);
                        break;
                    }
                }
            }
            if (candidates.Count == 1)
                return view.IncludeDesignStyles(new Uri(
                    $"/{candidates[0]};component/Design/SharedStyles.xaml", UriKind.Relative));
            throw new InvalidOperationException(
                $"Source=\".\" found {candidates.Count} shared-style assemblies ({string.Join(", ", candidates)}). " +
                "Specify /YourModule;component/Design/SharedStyles.xaml when the designer omits the page identity and the resource is ambiguous or missing.");
        }
        var assemblyName = assembly.GetName().Name;
        return view.IncludeDesignStyles(
            new Uri($"/{assemblyName};component/Design/SharedStyles.xaml", UriKind.Relative));
    }

    /// <summary>
    /// 仅在设计模式下合并指定资源，支持组件 URI 和绝对 pack URI。
    /// 例如 /MyModule;component/Design/SharedStyles.xaml。
    /// 资源加载失败时保留异常，让设计器显示具体路径问题。
    /// </summary>
    public static T IncludeDesignStyles<T>(this T view, Uri resourceUri) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!IsDesignMode(view))
            return view;

        ArgumentNullException.ThrowIfNull(resourceUri);
        view.Dispatcher.VerifyAccess();
        // Rider 会序列化页面中的 FrameworkTemplate。完整主题挂在页面资源上
        // 可能被一并遍历，导致 MarkupWriter.RecordNamespaces 递归溢出。
        // 设计器具有独立 Application，主题放在应用作用域且仍只在设计模式加载。
        var dictionaries = (Application.Current?.Resources ?? view.Resources).MergedDictionaries;
        if (dictionaries.Any(dictionary => dictionary.Source == resourceUri))
            return view;

        var resources = new ResourceDictionary { Source = resourceUri };
        // 应用中已有字典以及页面本地资源仍可以覆盖设计器默认样式。
        dictionaries.Insert(0, resources);
        return view;
    }
}



