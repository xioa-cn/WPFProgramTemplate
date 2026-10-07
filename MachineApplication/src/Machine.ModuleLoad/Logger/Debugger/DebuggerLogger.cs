using System.Drawing;
using System.Globalization;
using System.IO;
using Machine.ModuleLoad.StartupTool;

namespace Machine.ModuleLoad.Logger.Debugger
{
    public class DebuggerLogger : ILogger
    {
        internal static readonly object OutputLock = new();
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
                if (!CmdTools.HasStandardOutput()) return;

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

                try
                {
                    if (CmdTools.SupportsColorSequences)
                    {
                        Console.WriteLine($"\u001b[38;2;{color.R};{color.G};{color.B}m{output}\u001b[0m");
                    }
                    else
                    {
                        Console.WriteLine(output);
                    }
                }
                catch (IOException) { }
            }
        }
    }
}
