using System.Text;

namespace MachineApplication.Create;

/// <summary>控制台展示；重定向输出时使用普通逐行文本。</summary>
internal static class ConsoleUi
{
    private static bool UseColor => !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public static void Header()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.WriteLine();
        Line("  🚀 MachineApplication · 模块创建器", ConsoleColor.Cyan);
        Line("  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", ConsoleColor.DarkCyan);
        Line("  创建 WPF 模块 · 配置语言资源 · 加入解决方案", ConsoleColor.DarkGray);
        Line("  按 Enter 使用默认值", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    public static void Step(int number, string title)
    {
        Console.WriteLine();
        Line($"  ◆ {number}/3  {title}", ConsoleColor.Cyan);
    }

    public static void Info(string text) => Line("  " + text, ConsoleColor.Gray);

    public static string Ask(string prompt, string fallback)
    {
        Write($"  › {prompt} ", ConsoleColor.White);
        Write($"[{fallback}]", ConsoleColor.Yellow);
        Write(": ", ConsoleColor.Cyan);
        var input = Console.ReadLine() ?? throw new OperationCanceledException("输入已结束。");
        return string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
    }

    public static void Success(string path)
    {
        Console.WriteLine();
        Line("  🎉 模块创建成功！", ConsoleColor.Green);
        Info("📁 " + path);
        Line("  已完成项目生成，并加入解决方案。", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    public static void Error(string message)
    {
        var previous = Console.ForegroundColor;
        try
        {
            if (!Console.IsErrorRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null)
                Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"  ❌ 创建失败：{message}");
        }
        finally
        {
            if (!Console.IsErrorRedirected) Console.ForegroundColor = previous;
        }
    }

    private static void Line(string text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }

    private static void Write(string text, ConsoleColor color)
    {
        if (!UseColor) { Console.Write(text); return; }
        var previous = Console.ForegroundColor;
        try { Console.ForegroundColor = color; Console.Write(text); }
        finally { Console.ForegroundColor = previous; }
    }
}



