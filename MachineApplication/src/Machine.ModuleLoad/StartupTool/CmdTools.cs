using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Machine.ModuleLoad.Logger;
using Machine.ModuleLoad.Logger.Debugger;
using Microsoft.Win32.SafeHandles;

namespace Machine.ModuleLoad.StartupTool;

public class CmdTools
{
    private static readonly ConsoleControlHandler ControlHandler = HandleConsoleControl;
    private static bool _ownsConsole;
    private static TextWriter? _previousOutput;
    private static TextWriter? _previousError;
    private static TextReader? _previousInput;
    private static StreamWriter? _consoleOutput;
    private static StreamReader? _consoleInput;
    private static IntPtr _previousOutputHandle;
    private static IntPtr _previousErrorHandle;
    private static IntPtr _previousInputHandle;

    public static bool IsConsoleOpen
    {
        get
        {
            lock (DebuggerLogger.OutputLock)
                return _ownsConsole && GetConsoleWindow() != IntPtr.Zero;
        }
    }

    public static void InitializeConsole()
    {
        if (Debugger.IsAttached && !HasStandardOutput()) OpenConsole();
    }

    [Obsolete("请使用 InitializeConsole。")]
    public static void StartupCmd() => InitializeConsole();

    public static void OpenConsole()
    {
        GlobalLogger.EnableDebuggerLogger();
        lock (DebuggerLogger.OutputLock)
        {
            if (_ownsConsole) return;
            if (GetConsoleWindow() != IntPtr.Zero)
                throw new InvalidOperationException("The application is already attached to an external console.");

            _previousOutput = Console.Out;
            _previousError = Console.Error;
            _previousInput = Console.In;
            _previousOutputHandle = GetStdHandle(-11);
            _previousErrorHandle = GetStdHandle(-12);
            _previousInputHandle = GetStdHandle(-10);
            if (!AllocConsole()) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var window = GetConsoleWindow();
                var menu = GetSystemMenu(window, false);
                if (menu == IntPtr.Zero || !DeleteMenu(menu, 0xF060, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                DrawMenuBar(window);
                if (!SetConsoleCtrlHandler(ControlHandler, true))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!SetConsoleOutputCP(65001) || !SetConsoleCP(65001))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var outputStream = OpenConsoleStream("CONOUT$", FileAccess.Write);
                _consoleOutput = new StreamWriter(outputStream, new UTF8Encoding(false)) { AutoFlush = true };
                var inputStream = OpenConsoleStream("CONIN$", FileAccess.Read);
                _consoleInput = new StreamReader(inputStream, new UTF8Encoding(false));
                if (!SetStdHandle(-11, outputStream.SafeFileHandle.DangerousGetHandle()) ||
                    !SetStdHandle(-12, outputStream.SafeFileHandle.DangerousGetHandle()) ||
                    !SetStdHandle(-10, inputStream.SafeFileHandle.DangerousGetHandle()))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                Console.SetOut(_consoleOutput);
                Console.SetError(_consoleOutput);
                Console.SetIn(_consoleInput);
                var outputHandle = outputStream.SafeFileHandle.DangerousGetHandle();
                if (GetConsoleMode(outputHandle, out var outputMode))
                    SetConsoleMode(outputHandle, outputMode | 0x0004);
                _ownsConsole = true;
            }
            catch
            {
                SetConsoleCtrlHandler(ControlHandler, false);
                FreeConsole();
                RestoreStandardStreams();
                throw;
            }
        }
    }

    public static void CloseConsole()
    {
        lock (DebuggerLogger.OutputLock)
        {
            if (!_ownsConsole) return;
            if (!FreeConsole()) throw new Win32Exception(Marshal.GetLastWin32Error());
            _ownsConsole = false;
            SetConsoleCtrlHandler(ControlHandler, false);
            RestoreStandardStreams();
        }
    }

    private static FileStream OpenConsoleStream(string name, FileAccess access)
    {
        var handle = CreateFile(name, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }
        try { return new FileStream(handle, access); }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static void RestoreStandardStreams()
    {
        SetStdHandle(-11, _previousOutputHandle);
        SetStdHandle(-12, _previousErrorHandle);
        SetStdHandle(-10, _previousInputHandle);
        Console.SetOut(_previousOutput ?? TextWriter.Null);
        Console.SetError(_previousError ?? TextWriter.Null);
        Console.SetIn(_previousInput ?? TextReader.Null);
        try { _consoleOutput?.Dispose(); }
        catch (IOException) { }
        try { _consoleInput?.Dispose(); }
        catch (IOException) { }
        _consoleOutput = null;
        _consoleInput = null;
        _previousOutput = null;
        _previousError = null;
        _previousInput = null;
    }

    internal static bool HasStandardOutput()
    {
        var handle = GetStdHandle(-11);
        return handle != IntPtr.Zero && handle != new IntPtr(-1) && GetFileType(handle) != 0;
    }

    internal static bool SupportsColorSequences =>
        !GetConsoleMode(GetStdHandle(-11), out var mode) || (mode & 0x0004) != 0;

    private static bool HandleConsoleControl(uint controlType) => controlType is 0 or 1;

    private delegate bool ConsoleControlHandler(uint controlType);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int standardHandle, IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleOutputCP(uint codePage);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCP(uint codePage);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteMenu(IntPtr menu, uint position, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawMenuBar(IntPtr window);
}
