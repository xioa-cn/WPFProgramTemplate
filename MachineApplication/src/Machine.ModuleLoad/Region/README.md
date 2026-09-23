# Region 导航

`Machine.ModuleLoad.Region` 是一个面向 WPF 的区域导航实现，接口设计参考 Prism。区域以 `ContentControl` 作为宿主，每个区域同时只显示一个活动视图，并拥有自己的导航服务和导航历史。视图和 ViewModel 必须显式注册到负责创建它们的服务容器。

## 1. 注册服务

使用 `WpfApplication.Create<T>()` 创建应用服务集合时，框架会注册以下服务：

```csharp
IRegionManager       // 区域管理器
RegionManager        // 区域管理器具体类型
NavigationService     // URI 路由注册表和兼容导航入口
INavigationService    // NavigationService 的兼容接口
INavigateAsync       // 默认指向 NavigationService
```

模块服务集合可以调用 `AddRootInfrastructureServices(rootProvider)` 共享这些应用级单例。

## 2. 注册区域

区域可以在代码中注册：

```csharp
var regions = serviceProvider.GetRequiredService<IRegionManager>();
regions.Register("MainRegion", mainContentControl);
```

也可以使用附加属性：

```xml
<ContentControl
    region:RegionManager.RegionName="MainRegion" />
```

区域注册后可以通过实时集合访问：

```csharp
IRegion region = regions.Regions["MainRegion"];
IRegion sameRegion = regions.GetRegion("MainRegion");

region.Add(preloadedView);
region.Activate(preloadedView);
region.Deactivate(preloadedView);
region.Remove(preloadedView);
```

`IRegion.Views` 和 `IRegion.ActiveViews` 是只读集合。当前 `ContentControl` 适配器最多有一个活动视图。

### 自定义区域切换动画

区域动画由 `IRegionAnimation` 控制。默认使用短时淡入，也可以在注册区域时传入策略，或直接修改区域的 `Animation` 属性：

```csharp
public sealed class SlideAnimation : IRegionAnimation
{
    public void Animate(RegionAnimationContext context)
    {
        var transform = new TranslateTransform { X = 40 };
        context.Host.RenderTransform = transform;

        var animation = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(240));
        animation.Completed += (_, _) => context.Host.RenderTransform = null;
        transform.BeginAnimation(TranslateTransform.XProperty, animation);

        // 连续导航时取消当前动画并恢复宿主状态。
        context.RegisterCleanup(() =>
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            context.Host.RenderTransform = null;
        });
    }
}

regions.Register("MainRegion", mainContentControl, new SlideAnimation());
// 已注册区域也可以替换策略：
regions.Regions["MainRegion"].Animation = RegionAnimation.None;
```

`RegionAnimation` 可直接配置默认淡入的持续时间和缓动函数，也可以继承并重写 `Animate`，或使用 `new RegionAnimation(context => { ... })` 传入委托。XAML 宿主可以使用 `RegionAnimation.SetAnimation(host, strategy)` 设置附加策略。动画执行期间区域内容已经同步更新；`RegionAnimationContext.CancellationToken` 和 `RegisterCleanup` 用于清理被下一次导航替代的动画。

如果模块需要自己的区域集合，可以从模块服务容器解析 `IRegionManager`。模块加载桥接会使用单元模块名称绑定它：

```csharp
var moduleRegions = moduleProvider.GetRequiredService<IRegionManager>();
// moduleRegions.ServiceContainerName == 当前单元模块名称
```

也可以显式按名称创建：

```csharp
var moduleRegions = rootRegions.CreateRegionManager("Common");
```

按类型注册的视图和构造函数依赖必须注册在这个命名容器中。框架不会跨模块容器隐式查找或自动构造未注册服务。

## 3. 注册 URI 路由

`NavigationService` 负责把 URI 路径映射到视图类型。视图应注册为瞬态，以便在导航目标不允许复用时创建新实例：

```csharp
var navigation = serviceProvider.GetRequiredService<NavigationService>();
navigation.Register("settings", typeof(SettingsView));
navigation.Register("users", typeof(UsersView), moduleName: "UserModule");
```

也可以把视图发现直接绑定到区域：

```csharp
regions.RegisterViewWithRegion("ToolsRegion", typeof(ToolsView));
regions.RegisterViewWithRegion("ToolsRegion", () => new HelpView());
```

区域发现支持先注册视图再注册区域。区域创建后，已登记的视图工厂会立即执行并加入区域。按类型注册时，视图及其构造函数依赖必须存在于当前模块服务容器，否则会立即抛出异常。

## 4. 发起导航

推荐使用 `IRegionManager.RequestNavigate`，因为它会等待确认、解析目标、切换视图并报告最终结果：

```csharp
var result = await regions.RequestNavigateAsync("MainRegion", "settings");
if (!result.Result)
    logger.LogError(result.Error, "导航失败");
```

也可以使用回调：

```csharp
regions.RequestNavigate("MainRegion", "settings", result =>
{
    if (result.Result)
        logger.LogInformation("导航完成：{Uri}", result.Context!.Uri);
    else if (result.Error is not null)
        logger.LogError(result.Error, "导航失败");
}, parameters: null);
```

`RequestNavigateAsync` 是 `NavigationExtensions` 提供的扩展方法。字符串 URI 也由扩展方法转换为 `Uri`。区域服务本身同样支持：

```csharp
await region.NavigationService.RequestNavigateAsync(new Uri("settings", UriKind.Relative));
```

路由必须先注册，并且当前调用应明确指定目标区域。直接调用 `NavigationService.RequestNavigate(Uri, ...)` 时，框架只会自动选择名为 `MainRegion` 的区域，或者在只有一个区域时选择该区域。

## 5. 导航参数和上下文

`NavigationParameters` 同时保存 URI 查询参数和对象参数。使用 `Add` 添加参数：

```csharp
var parameters = new NavigationParameters()
    .Add("id", 42)
    .Add("payload", request);

regions.RequestNavigate("MainRegion", "settings?id=1", result => { }, parameters);
```

导航 URI 中的查询参数会自动解析。显式传入的对象参数会替换同名查询键：

```csharp
// id == 42，title == "overview"
regions.RequestNavigate(
    "MainRegion",
    "settings?id=1&title=overview",
    parameters: new NavigationParameters().Add("id", 42));
```

参数集合支持重复查询键、URL 解码、枚举、`Guid`、可空类型和基础类型转换：

```csharp
int id = context.Parameters.GetValue<int>("id");
string? title = context.Parameters.GetValueOrDefault<string>("title");
IEnumerable<int?> ids = context.Parameters.GetValues<int?>("id");
```

`RegionNavigationContext` 提供本次导航的完整信息：

```csharp
public sealed class SettingsViewModel : NavigationObservableObject
{
    public override void OnNavigatedTo(RegionNavigationContext context)
    {
        var id = context.Parameters.GetValue<int>("id");
        IRegion region = context.Region;
        IRegionNavigationService service = context.NavigationService;
    }
}
```

上下文包含 `NavigationService`、`Uri`、`Parameters` 和实际执行导航的 `IRegion`。`RegionName` 是该区域的注册名。

## 6. 导航感知和确认

视图或视图的 `DataContext` 可以实现 `INavigationAware`：

```csharp
public sealed class SettingsViewModel : NavigationObservableObject
{
    public override bool IsNavigationTarget(RegionNavigationContext context)
        => true;

    public override void OnNavigatedTo(RegionNavigationContext context) { }

    public override void OnNavigatedFrom(RegionNavigationContext context) { }
}
```

同一实例同时被视图和 `DataContext` 引用时只通知一次。`IsNavigationTarget` 返回 `false` 时，导航服务会创建新的目标实例。

需要阻止未保存内容离开的页面可以实现 `IConfirmNavigationRequest`：

```csharp
public sealed class EditorViewModel : NavigationObservableObject
{
    public override void ConfirmNavigationRequest(
        RegionNavigationContext context,
        Action<bool> continuationCallback)
    {
        ShowConfirmDialog(allow => continuationCallback(allow));
    }
}
```

确认回调可以稍后调用，也可以从后台线程调用。导航服务会切回区域 Dispatcher，并忽略重复回调。任何一个活动视图或其数据上下文拒绝确认，整个导航都会取消，目标视图也不会创建。

## 7. KeepAlive 和视图生命周期

视图或 `DataContext` 可以实现 `IRegionMemberLifetime`：

```csharp
public sealed class TemporaryViewModel : NavigationObservableObject, IRegionMemberLifetime
{
    public bool KeepAlive => false;
}
```

也可以使用特性：

```csharp
[RegionMemberLifetime(KeepAlive = false)]
public sealed class TemporaryView : UserControl { }
```

判断顺序为：视图实现、`DataContext` 实现、视图特性、`DataContext` 特性。`KeepAlive = false` 的视图离开区域后会被移除，再次通过历史或 URI 导航进入时按路由重新创建。

## 8. 导航历史

每个区域都有独立的 `IRegionNavigationJournal`：

```csharp
var journal = regions.Regions["MainRegion"].NavigationService.Journal;

if (journal.CanGoBack)
    journal.GoBack();

if (journal.CanGoForward)
    journal.GoForward();
```

历史项保存 URI 和参数，不会强引用普通路由视图。直接实例导航只保存视图的弱引用和类型信息；实例仍然存在并允许复用时，后退/前进会恢复原实例，否则会按类型创建新实例。

后退后发起新导航会清除前进分支。只有导航成功后历史游标才移动，确认取消或目标解析失败不会改变历史。`NavigationTarget` 可以替换为其他 `INavigateAsync` 实现，替换目标成功后才提交游标。

## 9. 兼容的同步入口

旧的同步入口仍然可用：

```csharp
UIElement view = navigation.Navigate("MainRegion", "settings", keepAlive: true);
```

该入口适合已经确定要返回视图的同步场景。它不能等待延迟确认；需要处理取消、异常和最终完成状态时，应使用 `RequestNavigate` 或 `RequestNavigateAsync`。

`RegionManager.Clear`、`IRegion.Remove`、区域注销和 `NavigationService.ClosePage` 属于直接管理操作，会取消当前等待中的确认回调并清理相应视图或历史。

## 10. 当前适配范围

- 区域宿主为 WPF `ContentControl`。
- 每个区域只有一个活动视图。
- 支持视图发现、URI 路由、异步确认、生命周期、子区域管理器和导航 Journal。
- 模块通过 `ModuleProvider` 按容器名称解析视图；子区域管理器不会隐式回退到其他模块容器。
- `Page` 会使用稳定的内部 `Frame` 宿主，避免 WPF 原生页面历史绕过区域历史。
- 没有实现 Prism 针对 `ItemsControl` 的 RegionAdapter、完整行为集合和所有平台特定扩展。
