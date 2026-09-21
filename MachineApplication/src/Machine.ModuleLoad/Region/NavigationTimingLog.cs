using System.IO;
using System.Threading.Channels;
using Machine.ModuleLoad.Logger;

namespace Machine.ModuleLoad.Region;

/// <summary>Temporary navigation diagnostics, independent of debugger trace listeners.</summary>
internal static class NavigationTimingLog
{
    // private static readonly Channel<string> Entries = Channel.CreateBounded<string>(
    //     new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    //
    // static NavigationTimingLog() => _ = Task.Run(WriteEntriesAsync);

    internal static void Write(string message) =>
        GlobalLogger.DebuggerLogger?.Trace($"{DateTimeOffset.Now:O} [pid={Environment.ProcessId}] {message}");

    // private static async Task WriteEntriesAsync()
    // {
    //     var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    //         "MachineApplication", "Logs");
    //     await foreach (var entry in Entries.Reader.ReadAllAsync())
    //     {
    //         try
    //         {
    //             Directory.CreateDirectory(directory);
    //             await File.AppendAllTextAsync(Path.Combine(directory, "NavigationTiming.log"),
    //                 entry + Environment.NewLine);
    //         }
    //         catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    //         {
    //             // Diagnostics must not interrupt navigation or block the UI thread.
    //             System.Diagnostics.Debug.WriteLine(exception.Message);
    //         }
    //     }
    // }
}