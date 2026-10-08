using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CsxPad.Wpf.Services;

namespace CsxPad.Wpf.Scripting;

internal static class ConsoleScriptHost
{
    internal const string ModeArgument = "--console-script-host";

    public static bool TryRun(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 2 ||
            !string.Equals(arguments[0], ModeArgument, StringComparison.Ordinal))
        {
            return false;
        }

        Run(arguments[1]);
        return true;
    }

    private static void Run(string pipeName)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.None);
        pipe.Connect(10_000);
        if (!TryReadMessage<ConsoleScriptRequest>(pipe, out var request))
        {
            return;
        }

        if (!NativeMethods.AllocConsole())
        {
            TryWriteMessage(pipe, new ConsoleScriptResponse(
                false,
                0,
                "Unable to allocate the script console.",
                []));
            return;
        }

        try
        {
            BindStandardStreams();
            Console.Title = "CsxPad Script Console";

            // WPF installs a dispatcher synchronization context on this startup thread.
            // Run the async script pipeline on a pool thread so continuations cannot deadlock startup.
            var result = Task.Run(async () =>
                {
                    var service = new CSharpScriptService(isolateConsoleScripts: false,
                        packageWorkspaceDirectory: request!.PackageWorkspaceDirectory);
                    return await service.RunAsync(request!.Code, request.ScriptPath, CancellationToken.None);
                })
                .GetAwaiter()
                .GetResult();
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                var previousColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(result.Output);
                Console.ForegroundColor = previousColor;
            }

            TryWriteMessage(pipe, CreateResponse(result));
        }
        catch (Exception exception)
        {
            TryDisplayHostError(exception);
            TryWriteMessage(pipe, new ConsoleScriptResponse(
                false,
                0,
                $"{exception.GetType().Name}: {exception.Message}",
                []));
        }
    }

    private static ConsoleScriptResponse CreateResponse(ScriptRunResult result)
    {
        var sections = result.ResultSections
            .Select(CreateResponseSection)
            .ToArray();
        return new ConsoleScriptResponse(
            result.Success,
            result.RowCount,
            result.Output,
            sections);
    }

    private static ConsoleScriptResultSection CreateResponseSection(ScriptResultSection section)
    {
        var table = section.View.Table;
        if (table is null)
        {
            return new ConsoleScriptResultSection(section.Title, section.Layout, [], []);
        }

        var columns = table.Columns.Cast<System.Data.DataColumn>()
            .Select(column => column.ColumnName)
            .ToArray();
        var rows = section.View.Cast<System.Data.DataRowView>()
            .Select(row => (IReadOnlyList<string?>)columns
                .Select(column => row[column] is DBNull ? null : row[column]?.ToString())
                .ToArray())
            .ToArray();
        return new ConsoleScriptResultSection(section.Title, section.Layout, columns, rows);
    }

    private static void BindStandardStreams()
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = encoding;
        Console.InputEncoding = encoding;
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), encoding, leaveOpen: true)
        {
            AutoFlush = true
        });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), encoding, leaveOpen: true)
        {
            AutoFlush = true
        });
        Console.SetIn(new StreamReader(
            Console.OpenStandardInput(),
            encoding,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true));
    }

    internal static bool TryWriteMessage<T>(Stream stream, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            var bytes = Encoding.UTF8.GetBytes(json);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    internal static bool TryReadMessage<T>(Stream stream, out T? value)
        where T : class
    {
        value = null;
        try
        {
            Span<byte> lengthBuffer = stackalloc byte[sizeof(int)];
            if (!TryReadExactly(stream, lengthBuffer))
            {
                return false;
            }

            var length = BitConverter.ToInt32(lengthBuffer);
            if (length is <= 0 or > 64 * 1024 * 1024)
            {
                return false;
            }

            var bytes = new byte[length];
            if (!TryReadExactly(stream, bytes))
            {
                return false;
            }

            value = JsonSerializer.Deserialize<T>(bytes);
            return value is not null;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or JsonException)
        {
            return false;
        }
    }

    private static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }

    private static void TryDisplayHostError(Exception exception)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
            Console.ResetColor();
        }
        catch (IOException)
        {
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllocConsole();
    }
}
