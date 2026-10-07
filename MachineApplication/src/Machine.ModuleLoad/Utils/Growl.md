# Growl 通知

主题感知的 WPF 通知宿主，不依赖 HandyControl。主窗口已挂载默认宿主，可在窗口加载后调用：

```csharp
using Machine.ModuleLoad.Utils;

Growl.Info("开始处理任务");
Growl.Success("保存成功");
Growl.Warning("请检查参数");
Growl.Error("连接失败");
Growl.Fatal("设备已停止，请人工确认");
Growl.Clear();
```

普通消息默认显示 4 秒；Fatal 默认常驻，需手动关闭。支持鼠标悬停及键盘焦点暂停计时、进入退出动画、最多 5 条通知（超过后移除最早的一条）、长消息自动换行和滚动。系统关闭客户端动画时不播放动画。

卡片背景、正文、分隔线及关闭按钮使用 Material Design 动态资源。Info 使用主题主色，Success 使用独立的绿色资源 `Growl.Brush.Success`（应用默认 `#22A06B`），Warning 使用主题辅色，Error / Fatal 使用主题错误色；Fatal 使用圆形感叹号图标。切换主题时卡片同步更新，成功色保持绿色，不随主色改变。独立使用该控件时需在应用资源中提供 `Growl.Brush.Success` 画刷；修改该动态资源会同步更新图标、浅色图标底和左侧状态条。

## 桌面全局通知

全局通知使用独立的置顶桌面浮层，显示在主显示器工作区右上角，不占任务栏、不抢焦点，也不属于主窗口的可视树。无需在 XAML 中挂载 Growl；主窗口最小化或隐藏时也可以显示，只要 WPF 应用及其 UI Dispatcher 仍在运行。

```csharp
Growl.InfoGlobal("后台任务开始");
Growl.SuccessGlobal("后台任务完成");
Growl.WarningGlobal("设备即将离线");
Growl.ErrorGlobal("连接已断开");
Growl.FatalGlobal("设备故障，请人工确认");
Growl.ClearGlobal();

Growl.ShowGlobal(new GrowlInfo
{
    Title = "后台任务",
    Message = "数据同步已完成。",
    Type = GrowlType.Success,
    Duration = TimeSpan.FromSeconds(6)
});
```

- 与窗口内通知复用同一套卡片、动画、悬停暂停、自动关闭和主题动态资源；主题资源须已加载到应用资源中。
- 普通全局消息默认 4 秒，FatalGlobal 默认常驻，最多 5 条；已显示的全局消息会跟随主题变化。
- `ClearGlobal()` 只清理桌面消息，`Clear()` / `Clear(token)` 只清理窗口内消息。`GrowlInfo.Token` 仅用于窗口内宿主，全局模式不按 Token 分组。
- 全部消息关闭后销毁桌面浮层，下次调用自动重建；应用退出时释放句柄和计时器。浮层不会加入 `Application.Windows`，不会被当成主窗口或阻止最后一个业务窗口关闭后退出。
- 定位按主屏工作区及 DPI 计算，避开任务栏，长消息受屏幕可用高度限制并支持滚动。
- 存在已加载的默认本地宿主时，关闭按钮文案绑定该宿主的 `CloseText`，继续跟随语言切换；独立使用时默认显示 Close。
- 使用应用 Dispatcher 派发，后台线程可以调用；仍应避免 UI 线程同步等待正在发送通知的后台任务。进程退出后的系统通知不在本功能范围内。

## 自定义通知

```csharp
Growl.Show(new GrowlInfo
{
    Title = "设备连接",
    Message = "连接已建立，可以开始操作。",
    Type = GrowlType.Success,
    Duration = TimeSpan.FromSeconds(6)
});
```

`Duration = TimeSpan.Zero` 表示常驻，`null` 使用宿主的 `DefaultDuration`。文字由调用方提供，可传入已有多语言资源；已经发送的消息保留发送时的文本。关闭按钮的 `CloseText` 可绑定动态语言资源，主窗口已绑定现有的“关闭”文案。

## 多宿主

在目标窗口的覆盖层 Grid 中放置控件（不要放在会挤占业务区域的 StackPanel 中）：

```xml
<utils:Growl Token="Device" Width="360" Margin="16"
             MaxCount="5" DefaultDuration="0:0:4" />
```

其中 `utils` 对应 `clr-namespace:Machine.ModuleLoad.Utils;assembly=Machine.ModuleLoad`。调用 `Growl.Success("连接成功", "Device")` 或 `Growl.Clear("Device")` 定向发送 / 清空，也可以通过控件实例的 `Push(GrowlInfo)` 投递。

静态方法支持后台线程调用，会同步派发至应用 UI 线程；不要在 UI 线程同步等待正在发送通知的后台任务。默认选择相同 Token 下的活动窗口宿主，否则选择最近加载的可见窗口宿主。窗口未加载或没有匹配宿主时会抛出明确异常，不隐式创建窗口。宿主卸载时停止计时器、取消动画并清空通知；空宿主隐藏，不遮挡主界面。

这不是 HandyControl 的完整 API 兼容实现；包含窗口内与桌面全局通知，不包含询问对话框或 Windows 通知中心推送。
