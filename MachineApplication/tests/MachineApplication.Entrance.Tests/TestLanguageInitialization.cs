using System.Runtime.CompilerServices;
using I18nExtensions;

namespace MachineApplication.Entrance.Tests;

internal static class TestLanguageInitialization
{
    // The test host does not execute the application's language initialization.
    [ModuleInitializer]
    internal static void Initialize() => LanguageManager.CreateInstance("zh");
}
