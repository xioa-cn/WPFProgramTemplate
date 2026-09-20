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

    public void ChangeLang(string langKey)
    {
        CurrentCulture = langKey;
        foreach (var item in _moduleLang)
        {
            item.SetCulture(CurrentCulture);
        }
    }
}