using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Windows;
using I18nExtensions;
using Machine.ModuleLoad;
using Machine.ModuleLoad.Logger;
using MachineApplication.Entrance.Theme;

namespace MachineApplication.Entrance;

public partial class App : Application, IWpfApp
{
    private bool _resourcesInitialized;

    /// <summary>创建应用，默认从 exe 同级的 Theme.json 读取主题。</summary>
    public App() : this(new ThemeSettingsService()) { }

    /// <summary>允许传入主题配置服务，便于使用独立文件验证启动流程。</summary>
    public App(ThemeSettingsService themeSettings)
    {
        ThemeSettings = themeSettings;
        LanguageManager.CreateInstance("zh");
    }

    /// <summary>应用共享的主题配置，页面修改与启动恢复使用同一份状态。</summary>
    public ThemeSettingsService ThemeSettings { get; }

    public void InitializeWpfComponent()
    {
        if (_resourcesInitialized) return;

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/MachineApplication.Entrance;component/AppResources.xaml",
                UriKind.Absolute)
        });

        // 必须先加载 MaterialDesign 资源再恢复主题，并在创建任何窗口之前完成。
        ThemeSettings.Load().Match(
            _ => { },
            error => GlobalLogger.Error($"Failed to load theme settings. Using the default theme. {error}"));
        _resourcesInitialized = true;
    }
}
