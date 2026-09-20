
using MachineApplication.Entrance.Resources;

namespace MachineApplication.Entrance.ViewModels
{
    public class ViewModelLocator
    {
        public static EntranceLang EntranceLang { get; } = CreateLanguage();

        private static EntranceLang CreateLanguage()
        {
            // The previewer does not execute application startup.
            if (Machine.ModuleLoad.Utils.ViewDesignInclude.IsDesignMode(new System.Windows.DependencyObject()))
                I18nExtensions.LanguageManager.CreateInstance("zh");
            return new EntranceLang();
        }
        
        internal ViewModelLocator()
        {
            
        }
    }
}
