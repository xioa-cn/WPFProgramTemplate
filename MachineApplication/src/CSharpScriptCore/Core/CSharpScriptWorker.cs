using System.Diagnostics;
using System.Text.Json;
using CSharpScriptCore.Models;

namespace CSharpScriptCore.Core;

/// <summary>
/// 在独立进程中运行脚本。超时或取消时会终止 Worker 进程树。
/// </summary>
public static class CSharpScriptWorker
{
    /// <summary>在 Worker 中执行代码字符串。</summary>
    public static Task<ScriptResult> ExecuteCodeAsync(
        string code,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(WorkerOperation.ExecuteCode, code, null, null, [], options, cancellationToken)
            .ContinueWith<ScriptResult>(task => task.GetAwaiter().GetResult(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <summary>在 Worker 中执行代码字符串并转换最后的表达式。</summary>
    public static Task<ScriptResult<T>> ExecuteCodeAsync<T>(
        string code,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<T>(WorkerOperation.ExecuteCode, code, null, null, [], options, cancellationToken);

    /// <summary>在 Worker 中执行 CSX 文件。</summary>
    public static Task<ScriptResult> ExecuteFileAsync(
        string filePath,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(WorkerOperation.ExecuteFile, filePath, null, null, [], options, cancellationToken)
            .ContinueWith<ScriptResult>(task => task.GetAwaiter().GetResult(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <summary>在 Worker 中执行 CSX 文件并转换最后的表达式。</summary>
    public static Task<ScriptResult<T>> ExecuteFileAsync<T>(
        string filePath,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<T>(WorkerOperation.ExecuteFile, filePath, null, null, [], options, cancellationToken);

    /// <summary>在 Worker 中调用代码声明的静态方法。</summary>
    public static Task<ScriptResult<T>> RunStaticMethodAsync<T>(
        string code,
        string className,
        string methodName,
        object[]? parameters = null,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<T>(WorkerOperation.RunStaticCode, code, className, methodName,
            parameters ?? [], options, cancellationToken);

    /// <summary>在 Worker 中调用 CSX 文件声明的静态方法。</summary>
    public static Task<ScriptResult<T>> RunStaticMethodFromFileAsync<T>(
        string filePath,
        string className,
        string methodName,
        object[]? parameters = null,
        ScriptWorkerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<T>(WorkerOperation.RunStaticFile, filePath, className, methodName,
            parameters ?? [], options, cancellationToken);

    private static async Task<ScriptResult<T>> ExecuteAsync<T>(
        WorkerOperation operation,
        string source,
        string? className,
        string? methodName,
        object[] parameters,
        ScriptWorkerOptions? workerOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        workerOptions ??= new ScriptWorkerOptions();
        var request = new WorkerRequest(
            operation,
            source,
            className,
            methodName,
            parameters.Select(value => WorkerValue.FromObject(value)!).ToArray(),
            WorkerExecutionOptions.FromOptions(workerOptions.ExecutionOptions));
        var requestPath = Path.Combine(Path.GetTempPath(), $"csx-worker-{Guid.NewGuid():N}.request.json");
        var responsePath = Path.Combine(Path.GetTempPath(), $"csx-worker-{Guid.NewGuid():N}.response.json");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(request, WorkerJson.Options),
                cancellationToken).ConfigureAwait(false);
            using var process = StartWorker(workerOptions, requestPath, responsePath);
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(workerOptions.ConsoleInput))
            {
                await process.StandardInput.WriteAsync(workerOptions.ConsoleInput).ConfigureAwait(false);
            }
            process.StandardInput.Close();

            using var timeoutSource = new CancellationTokenSource(workerOptions.Timeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutSource.Token);
            try
            {
                await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                var partialOutput = await standardOutputTask.ConfigureAwait(false);
                var partialError = await standardErrorTask.ConfigureAwait(false);
                workerOptions.StandardOutputReceived?.Invoke(partialOutput);
                workerOptions.StandardErrorReceived?.Invoke(partialError);
                var status = cancellationToken.IsCancellationRequested
                    ? ScriptRunStatus.Cancelled
                    : ScriptRunStatus.Timeout;
                var message = status == ScriptRunStatus.Timeout
                        ? $"Worker exceeded the hard timeout of {workerOptions.Timeout}."
                        : "Worker execution was cancelled.";
                var output = string.Join(Environment.NewLine,
                    new[] { message, partialOutput, partialError }
                        .Where(text => !string.IsNullOrWhiteSpace(text)));
                return Failure<T>(status, message, stopwatch.ElapsedMilliseconds, output: output);
            }

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            workerOptions.StandardOutputReceived?.Invoke(standardOutput);
            workerOptions.StandardErrorReceived?.Invoke(standardError);

            if (!File.Exists(responsePath))
            {
                return Failure<T>(ScriptRunStatus.HostInternalError,
                    $"Worker exited with code {process.ExitCode} without a response. {standardError}".Trim(),
                    stopwatch.ElapsedMilliseconds);
            }

            var response = JsonSerializer.Deserialize<WorkerResponse>(
                await File.ReadAllTextAsync(responsePath, CancellationToken.None).ConfigureAwait(false),
                WorkerJson.Options) ?? throw new InvalidOperationException("Worker returned an empty response.");
            return ConvertResponse<T>(response, standardOutput, standardError);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<T>(ScriptRunStatus.HostInternalError,
                $"{exception.GetType().Name}: {exception.Message}", stopwatch.ElapsedMilliseconds, exception);
        }
        finally
        {
            TryDelete(requestPath);
            TryDelete(responsePath);
        }
    }

    private static Process StartWorker(
        ScriptWorkerOptions options,
        string requestPath,
        string responsePath)
    {
        var workerPath = ResolveWorkerPath(options.WorkerPath);
        var isDll = string.Equals(Path.GetExtension(workerPath), ".dll", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo(isDll ? "dotnet" : workerPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory
        };
        if (isDll) startInfo.ArgumentList.Add(workerPath);
        startInfo.ArgumentList.Add(requestPath);
        startInfo.ArgumentList.Add(responsePath);
        foreach (var item in options.EnvironmentVariables)
        {
            startInfo.Environment[item.Key] = item.Value;
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start script worker.");
    }

    private static string ResolveWorkerPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "CSharpScriptCore.Worker.exe"),
            Path.Combine(AppContext.BaseDirectory, "CSharpScriptCore.Worker.dll")
        };
        return candidates.FirstOrDefault(File.Exists) ??
               throw new FileNotFoundException(
                   "CSharpScriptCore.Worker was not found. Deploy the Worker project output beside the host or set WorkerPath.");
    }

    private static ScriptResult<T> ConvertResponse<T>(
        WorkerResponse response,
        string standardOutput,
        string standardError)
    {
        object? value = null;
        Exception? conversionException = null;
        if (response.ReturnValue is not null)
        {
            try
            {
                value = response.ReturnValue.Deserialize(typeof(T));
            }
            catch (Exception exception)
            {
                conversionException = exception;
            }
        }

        var output = string.Join(Environment.NewLine,
            new[] { response.Output, standardOutput, standardError }.Where(text => !string.IsNullOrWhiteSpace(text)));
        if (conversionException is not null)
        {
            return Failure<T>(ScriptRunStatus.RuntimeException,
                $"Worker result cannot be converted to '{typeof(T).FullName}': {conversionException.Message}",
                response.ElapsedMilliseconds, conversionException, response.Diagnostics,
                response.StructuredDiagnostics, output, response.Timings);
        }

        return new ScriptResult<T>
        {
            RunResult = response.RunResult,
            Exception = response.ExceptionMessage is null
                ? null
                : new ScriptWorkerException(response.ExceptionType, response.ExceptionMessage),
            ElapsedMilliseconds = response.ElapsedMilliseconds,
            Diagnostics = response.Diagnostics,
            StructuredDiagnostics = response.StructuredDiagnostics,
            Output = output,
            ReturnValue = value,
            Data = value is T typed ? typed : default,
            Timings = response.Timings
        };
    }

    private static ScriptResult<T> Failure<T>(
        ScriptRunStatus status,
        string message,
        long elapsed,
        Exception? exception = null,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<ScriptDiagnostic>? structuredDiagnostics = null,
        string? output = null,
        ScriptExecutionTimings? timings = null) => new()
    {
        RunResult = status,
        Exception = exception ?? new ScriptWorkerException(null, message),
        ElapsedMilliseconds = elapsed,
        Diagnostics = diagnostics ?? [],
        StructuredDiagnostics = structuredDiagnostics ?? [],
        Output = output ?? message,
        Timings = timings
    };

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Worker 边界返回的远程异常。</summary>
public sealed class ScriptWorkerException(string? remoteType, string message) : Exception(message)
{
    /// <summary>Worker 中原始异常的完整类型名。</summary>
    public string? RemoteType { get; } = remoteType;
}
