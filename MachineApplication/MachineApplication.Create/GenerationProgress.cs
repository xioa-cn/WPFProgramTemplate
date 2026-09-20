using System.Diagnostics;

namespace MachineApplication.Create;

/// <summary>按实际工作分段推进，完整动画至少持续 5.2 秒。</summary>
internal sealed class GenerationProgress : IDisposable
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly int total;
    private int completed;
    private int frame;
    private int lastLength;
    private bool lineOpen;
    private readonly bool interactive = !Console.IsOutputRedirected;
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    public GenerationProgress(int total) => this.total = total;

    public async Task RunStepAsync(string label, Func<Task> action)
    {
        var work = action();
        var start = completed * 100.0 / total;
        var end = (completed + 1) * 100.0 / total;
        var stepClock = Stopwatch.StartNew();
        var duration = 5200.0 / total;
        try
        {
            do
            {
                var fraction = Math.Min(stepClock.Elapsed.TotalMilliseconds / duration, 0.95);
                Render(start + (end - start) * fraction, label);
                if (work.IsFaulted || work.IsCanceled) await work;
                await Task.Delay(40);
            } while (!work.IsCompleted || stepClock.Elapsed.TotalMilliseconds < duration);
            await work;
            completed++;
            Render(end, completed == total ? "生成完成" : label);
            if (!interactive)
                Console.WriteLine($"  ✓ {completed}/{total}  {label}  ·  {clock.Elapsed.TotalSeconds:F1}s");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Render(double percent, string label)
    {
        if (!interactive) return;
        var previous = Console.ForegroundColor;
        var color = Environment.GetEnvironmentVariable("NO_COLOR") is null;
        try
        {
            // 保守限制宽度，避免中文路径导致终端自动换行。
            var width = 90;
            try { if (Console.WindowWidth > 0) width = Console.WindowWidth; } catch (IOException) { }
            var barWidth = width < 75 ? 12 : 26;
            if (label.Length > 16) label = label[..15] + "…";
            var cells = percent / 100 * barWidth;
            var full = (int)cells;
            var parts = " ▏▎▍▌▋▊▉";
            var bar = new string('█', full);
            if (full < barWidth)
                bar += parts[(int)((cells - full) * 8)] + new string('░', barWidth - full - 1);
            var icon = percent >= 100 ? "✓" : Frames[frame++ % Frames.Length];
            var text = width < 55
                ? $"  {icon} {percent,3:F0}% · {clock.Elapsed.TotalSeconds:F1}s"
                : $"  {icon} {bar} {Math.Floor(percent),3:F0}% · {clock.Elapsed.TotalSeconds:F1}s · {label}";
            Console.Write('\r');
            if (color) Console.ForegroundColor = percent >= 100 ? ConsoleColor.Green : ConsoleColor.Cyan;
            Console.Write(text);
            Console.Write(new string(' ', Math.Max(0, lastLength - text.Length)));
            lastLength = text.Length;
            lineOpen = true;
        }
        finally { if (color) Console.ForegroundColor = previous; }
    }

    public void Dispose()
    {
        if (lineOpen) { Console.WriteLine(); lineOpen = false; }
    }
}

