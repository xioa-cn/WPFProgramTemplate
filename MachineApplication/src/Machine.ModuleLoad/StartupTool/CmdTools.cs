using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Machine.ModuleLoad.StartupTool;

public class CmdTools
{
    public static void InitializeConsole()
    {
        InitializeConsoleWindow();
    }

    [Obsolete("请使用 InitializeConsole。")]
    public static void StartupCmd() => InitializeConsole();

    private static void InitializeConsoleWindow()
    {
        if (Debugger.IsAttached && !HasStandardOutput())
        {
            AllocConsole();
        }
    }
    
    private static bool HasStandardOutput()
    {
        const int standardOutputHandle = -11;
        const uint unknownFileType = 0;

        // IsOutputRedirected also returns true when a WinExe has no console.
        var handle = GetStdHandle(standardOutputHandle);
        return handle != IntPtr.Zero
               && handle != new IntPtr(-1)
               && GetFileType(handle) != unknownFileType;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();
}
