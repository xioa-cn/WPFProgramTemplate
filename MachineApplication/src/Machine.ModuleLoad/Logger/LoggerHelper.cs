namespace Machine.ModuleLoad.Logger;

public static class LoggerHelper
{
    public static string ClaLog(this string className, string message)
    {
        return $"[{className}] {message}";
    }
}