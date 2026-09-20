using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace I18nExtensions
{
    public abstract class LangBase : INotifyPropertyChanged
    {
        private Dictionary<string, Dictionary<string, string>>? _langStore;

        public void SetLangInfo(string culture, string key, string value)
        {
            if (_langStore == null)
            {
                _langStore = new Dictionary<string, Dictionary<string, string>>();
            }

            if (!_langStore.TryGetValue(culture, out Dictionary<string, string>? map) || map == null)
            {
                map = _langStore[culture] = new Dictionary<string, string>();
            }

            map[key] = value;
        }

        public string GetValue(string key)
        {
            if (_langStore == null)
            {
                return $"{key}";
            }

            if (!_langStore.TryGetValue(LanguageManager.Instance.CurrentCulture, out Dictionary<string, string>? map) 
                || map == null || !map.TryGetValue(key,out string? resultMsg))
            {
                return $"{key}";
            }

            return resultMsg ?? $"{key}";
        }

        protected LangBase()
        {
            LanguageManager.Instance.AddModuleLang(this);

            Initialized();

            SetCulture(LanguageManager.Instance.CurrentCulture);
        }
        private string? _currentCulture;
        public void SetCulture(string culture)
        {
            if (_currentCulture == culture) return;

            _currentCulture = culture;
            // 通知所有属性变更，WPF绑定全部刷新
            OnPropertyChanged(string.Empty);
        }

        protected virtual void Initialized()
        {
            
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
