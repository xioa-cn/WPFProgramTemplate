# 主题消息框

`XMessageBox.Show` 提供与 WPF `System.Windows.MessageBox.Show` 对应的 12 个重载，
使用相同的 `MessageBoxButton`、`MessageBoxImage`、`MessageBoxResult` 和 `MessageBoxOptions`。
项目中的业务消息框统一使用 `XMessageBox.Show`；新增调用也应使用此入口。
仅组件内部的特殊桌面模式保留原生 `MessageBox.Show` 回退，避免递归调用。

```csharp
using Machine.ModuleLoad.Mvvm;
using System.Windows;

var result = XMessageBox.Show(
    "配置尚未保存，是否保存后离开？", "系统配置",
    MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
if (result == MessageBoxResult.Yes)
{
    SaveConfiguration();
}
```

可以在第一个参数传入 owner 窗口；不指定时使用当前活动的应用窗口。
消息框模态显示并居中于 owner，后台调用自动切回 owner 或 Application 的 Dispatcher。
无 WPF Application 且调用线程不是 STA 时，创建短生命周期的 STA 线程显示。
同步 Show 会阻塞调用方，不要在 UI 线程阻塞等待一个正在调用 Show 的后台任务。

- 支持 OK、OKCancel、YesNo、YesNoCancel，默认按钮不在按钮组内时使用第一个按钮。
- 支持 None、Information、Warning、Error、Question 及原生枚举别名，并播放对应系统提示音。
- 支持 Enter、Space、Tab、左右方向键、Alt 访问键以及 Ctrl+C 复制标题、正文和按钮文字。
- OK 框关闭/Esc 返回 OK；带 Cancel 的消息框关闭/Esc 返回 Cancel；YesNo 禁止关闭/Esc/Alt+F4。
- 长正文自动换行并滚动，按钮区保持可见；RightAlign、RtlReading 应用于正文。
- 普通消息框使用与宿主相反的明暗配色：浅色页面显示深色消息框，深色页面显示浅色消息框。
  每次打开时根据宿主卡片背景判断明暗，保留宿主主色与辅助色，完整切换正文、按钮及边框资源。
  主题资源仅合并到消息框，不修改应用或宿主资源；无宿主主题时按浅色页面处理，显示深色消息框。
- 按钮根据 CurrentUICulture 使用中文或英文。
- ServiceNotification、DefaultDesktopOnly 需要操作系统桌面语义，回退原生 MessageBox，
  这两种模式不做主题化，也不能指定 owner。

此组件兼容 WPF 消息框调用，不包含 WinForms MessageBox 的帮助按钮、IWin32Window 等扩展。
