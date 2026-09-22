using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace MachineApplication.Entrance.ViewModels;

/// <summary>已打开的页面标签；标题和图标从当前菜单配置更新。</summary>
public partial class WorkspaceTab(string url) : ObservableObject
{
    public string Url { get; } = url;
    [ObservableProperty] private string _title = url;
    [ObservableProperty] private PackIconKind _icon = PackIconKind.FileOutline;
    [ObservableProperty] private bool _isSelected;
}
