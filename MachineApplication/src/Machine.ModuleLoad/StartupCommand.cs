namespace Machine.ModuleLoad;

public class StartupCommand(string[]? args)
{
    public string[]? Args { get; } = args;
}