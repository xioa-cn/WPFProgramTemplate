using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using CsxPad.Wpf.Scripting;

namespace CsxPad.Wpf.Services;

internal static class ConsoleScriptProcessRunner
{
    public static async Task<ScriptRunResult> RunAsync(
        string code,
        string? scriptPath,
        CancellationToken cancellationToken)
    {
        var pipeName = $"CsxPad.Console.{Environment.ProcessId}.{Guid.NewGuid():N}";
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var process = StartHost(pipeName);
        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));

        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken);
            if (!ConsoleScriptHost.TryWriteMessage(pipe, new ConsoleScriptRequest(code, scriptPath)))
            {
                return ScriptRunResult.Failed("The script console was closed.");
            }

            var response = await Task.Run(
                () => ConsoleScriptHost.TryReadMessage<ConsoleScriptResponse>(pipe, out var message)
                    ? message
                    : null,
                cancellationToken);
            if (response is null)
            {
                return ScriptRunResult.Failed("The script console was closed.");
            }

            return CreateResult(response);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return ScriptRunResult.Failed("The script console was closed.");
        }
        finally
        {
            TryKill(process);
        }
    }

    private static Process StartHost(string pipeName)
    {
        var assemblyPath = typeof(ConsoleScriptHost).Assembly.Location;
        var appHostPath = Path.ChangeExtension(assemblyPath, ".exe");
        var startInfo = new ProcessStartInfo(File.Exists(appHostPath) ? appHostPath : "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (!File.Exists(appHostPath))
        {
            startInfo.ArgumentList.Add(assemblyPath);
        }

        startInfo.ArgumentList.Add(ConsoleScriptHost.ModeArgument);
        startInfo.ArgumentList.Add(pipeName);
        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("Unable to start the console script process.");
    }

    private static ScriptRunResult CreateResult(ConsoleScriptResponse response)
    {
        var sections = response.ResultSections.Select(CreateResultSection).ToArray();
        var primarySection = sections.LastOrDefault() ?? ScriptResultSection.Empty;

        return new ScriptRunResult(
            response.Success,
            primarySection.View,
            response.RowCount,
            response.Output,
            primarySection.Layout,
            sections);
    }

    private static ScriptResultSection CreateResultSection(ConsoleScriptResultSection response)
    {
        var table = new DataTable();
        foreach (var column in response.Columns)
        {
            table.Columns.Add(column, typeof(object));
        }

        foreach (var row in response.Rows)
        {
            table.Rows.Add(row.Select(value => (object?)value ?? DBNull.Value).ToArray());
        }

        if (table.Columns.Count == 0)
        {
            table.Columns.Add("Result", typeof(object));
        }

        return new ScriptResultSection(
            response.Title,
            table.DefaultView,
            table.Rows.Count,
            response.Layout);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
