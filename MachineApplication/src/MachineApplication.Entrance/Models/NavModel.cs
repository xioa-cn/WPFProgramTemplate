using Machine.ModuleLoad.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Models;

public partial class NavModel : ViewModelBase
{
    /// <summary>创建菜单节点；分组配置 Children，叶节点配置已注册的 Url。</summary>
    public NavModel(string languageKey, PackIconKind icon, string? url = null, params NavModel[] children)
    {
        LanguageKey = languageKey;
        Icon = icon;
        Url = url;
        Children = children;
    }
    public string LanguageKey { get; }
    public string Title => ViewModelLocator.EntranceLang.GetValue(LanguageKey);
    public PackIconKind Icon { get; }
    public string? Url { get; }
    public IReadOnlyList<NavModel> Children { get; }
    public bool HasChildren => Children.Count > 0;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    /// <summary>语言切换时递归刷新所有菜单标题。</summary>
    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Title));
        foreach (var child in Children) child.RefreshLanguage();
    }

    /// <summary>同步选中路由，并展开通向该页面的父节点。</summary>
    public bool SelectRoute(string url)
    {
        IsSelected = Url == url;
        var containsSelection = IsSelected;
        foreach (var child in Children) containsSelection |= child.SelectRoute(url);
        if (HasChildren && containsSelection) IsExpanded = true;
        return containsSelection;
    }
}
