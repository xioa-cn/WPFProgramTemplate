namespace I18nExtensions;

public class LanguageManager
{
    public static LanguageManager Instance
    {
        get => field ?? throw new NullReferenceException();
        private set;
    }

    public static LanguageManager CreateInstance(string culture)
    {
        Instance = new LanguageManager(culture);
        return Instance;
    }

    public LanguageManager(string currentCulture)
    {
        CurrentCulture = currentCulture;
    }

    private List<LangBase> _moduleLang = new List<LangBase>();
    public string CurrentCulture { get; private set; }

    public void AddModuleLang(LangBase moduleLang)
    {
        _moduleLang.Add(moduleLang);
    }

    /// <summary>汇总已加载模块的语言，按标识去重；每次查询都包含后来加载的模块资源。</summary>
    public IReadOnlyList<string> GetAvailableCultures() => _moduleLang
        .SelectMany(module => module.GetAvailableCultures())
        .Where(culture => !string.IsNullOrWhiteSpace(culture))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(culture => culture, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void ChangeLang(string langKey)
    {
        CurrentCulture = langKey;
        foreach (var item in _moduleLang)
        {
            item.SetCulture(CurrentCulture);
        }
    }
}
