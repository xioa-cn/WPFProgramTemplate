using System.Drawing;
using System.Globalization;
using ColorConsole = Colorful.Console;

namespace Machine.ModuleLoad.Logger.Debugger
{
    public class DebuggerLogger : ILogger
    {
        private static readonly object OutputLock = new();
        private const int LevelWidth = 7;

        public void Trace(string message) => Write("TRACE", message, Color.Gray);

        public void Debug(string message) => Write("DEBUG", message, Color.Cyan);

        public void Info(string message) => Write("INFO", message, Color.White);

        public void Success(string message) => Write("SUCCESS", message, Color.LimeGreen);

        public void Banner(string message) => Write("SUCCESS", message, Color.HotPink, header: false);

        public void Warn(string message) => Write("WARN", message, Color.Yellow);

        public void Error(string message, Exception? exception = null) =>
            Write("ERROR", message, Color.Red, exception);

        public void Fatal(string message, Exception? exception = null) =>
            Write("FATAL", message, Color.Magenta, exception);

        private static void Write(string level, string message, Color color, Exception? exception = null, bool header = true)
        {
            lock (OutputLock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

                var output = "";
                if (header)
                {
                    output = $"[{timestamp}] [{level.PadRight(LevelWidth)}] {message}";
                }
                else
                {
                    output = $" {message}";
                }

                if (exception != null)
                {
                    output += Environment.NewLine + exception;
                }

                // Colorful changes shared console state; keep each complete entry under one lock.
                if (Console.IsOutputRedirected)
                {
                    // IDE consoles receive a text stream and need ANSI sequences for colors.
                    Console.WriteLine($"\u001b[38;2;{color.R};{color.G};{color.B}m{output}\u001b[0m");
                }
                else
                {
                    ColorConsole.WriteLine(output, color);
                }
            }
        }
    }
}
