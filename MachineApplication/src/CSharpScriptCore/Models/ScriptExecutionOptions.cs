namespace CSharpScriptCore.Models;

/// <summary>
/// CSX 脚本执行选项。集合属性可直接添加外部程序集引用和默认命名空间。
/// </summary>
public sealed class ScriptExecutionOptions
{
    /// <summary>脚本文件路径。设置后，相对 <c>#load</c> 将以脚本所在目录解析。</summary>
    public string? ScriptPath { get; set; }

    /// <summary>未指定脚本路径时使用的基础目录。</summary>
    public string BaseDirectory { get; set; } = Environment.CurrentDirectory;

    /// <summary>NuGet 工作目录。默认位于应用输出目录的 <c>Data/NuGet</c>。</summary>
    public string PackageWorkspaceDirectory { get; set; } = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "NuGet");

    /// <summary>需要额外引用的程序集文件路径。</summary>
    public ICollection<string> References { get; } = new List<string>();

    /// <summary>脚本默认导入的命名空间。</summary>
    public ICollection<string> Imports { get; } = new List<string>();

    /// <summary>脚本可访问的全局对象。其公开成员可在脚本顶层直接使用。</summary>
    public object? Globals { get; set; }

    /// <summary>是否自动引用当前进程已加载的程序集。默认启用。</summary>
    public bool IncludeLoadedAssemblies { get; set; } = true;

    /// <summary>是否处理 <c>#r "nuget: 包名, 版本"</c> 指令。默认启用。</summary>
    public bool EnableNuGetDirectives { get; set; } = true;

    /// <summary>附加 NuGet 源。为空时使用计算机的 NuGet.Config。</summary>
    public ICollection<string> NuGetSources { get; } = new List<string>();

    /// <summary>可选 NuGet.Config 文件路径，适用于私有源和认证配置。</summary>
    public string? NuGetConfigFile { get; set; }

    /// <summary>
    /// 依赖还原使用的运行时标识符，例如 <c>win-x64</c>。
    /// 未设置时自动使用当前进程 RID；显式设置可覆盖自动检测结果。
    /// </summary>
    public string? RuntimeIdentifier { get; set; }

    /// <summary>是否使用进程内编译缓存。默认启用。</summary>
    public bool EnableCompilationCache { get; set; } = true;

    /// <summary>可选执行超时。不设置时仅响应调用方的取消令牌。</summary>
    public TimeSpan? Timeout { get; set; }
}
