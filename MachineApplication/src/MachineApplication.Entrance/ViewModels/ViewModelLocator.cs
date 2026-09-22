
using MachineApplication.Entrance.Resources;

namespace MachineApplication.Entrance.ViewModels
{
    /// <summary>提供 XAML 可静态引用的共享语言对象，并处理设计器初始化。</summary>
    public class ViewModelLocator
    {
        public static EntranceLang EntranceLang { get; } = CreateLanguage();

        /// <summary>为界面创建语言资源对象，设计器环境下先初始化默认语言。</summary>
        private static EntranceLang CreateLanguage()
        {
            // 设计器不会执行应用启动流程，因此需要单独初始化语言管理器。
            if (Machine.ModuleLoad.Utils.ViewDesignInclude.IsDesignMode(new System.Windows.DependencyObject()))
                I18nExtensions.LanguageManager.CreateInstance("zh");
            return new EntranceLang();
        }
        
        /// <summary>保留模块内部构造入口，界面通常通过静态属性访问语言对象。</summary>
        internal ViewModelLocator()
        {
            
        }
    }
}
