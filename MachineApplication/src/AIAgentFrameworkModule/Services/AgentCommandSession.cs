using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AIAgentFrameworkModule.Services;

public sealed record AgentCommandApproval(string Role, string Command, string WorkingDirectory, string Reason, string Executable)
{
    public TaskCompletionSource<bool> Decision { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string Preview => $"角色：{Role}\n解释器：{Executable}\n工作目录：{WorkingDirectory}\n目的：{Reason}\n\n{Command}";
}

public sealed class AgentCommandSession(
    string root,
    Func<AgentCommandApproval, CancellationToken, Task<bool>> approve,
    IProgress<string> progress)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly List<string> history = [];
    public string Root { get; } = AgentWorkspaceWriter.ValidateRoot(root);

    public string History
    {
        get { lock (history) return JsonSerializer.Serialize(history); }
    }

    public IList<AITool> CreateTools(string role) =>
    [
        AIFunctionFactory.Create(
            (string command, string workingDirectory, string reason, CancellationToken cancellationToken) =>
                ExecuteAsync(role, command, workingDirectory, reason, cancellationToken),
            "execute_command",
            "执行 Windows PowerShell 命令，支持本机已安装的 dotnet、node、npm、python、pip、git 等。" +
            "command 为完整命令；workingDirectory 为授权根目录内已存在的相对目录（根目录用 .）；reason 为中文目的。" +
            "每次都须用户确认，返回真实退出码、stdout 和 stderr。无自动超时；禁止启动后台或交互进程。" +
            "被拒绝后不得换写法绕过；失败后根据输出诊断，不得声称成功。")
    ];

    private string ResolveDirectory(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) relative = ".";
        if (Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new ArgumentException("工作目录必须是授权目录内的相对路径。");
        string target = Path.GetFullPath(Path.Combine(Root, relative));
        if (!target.Equals(Root, StringComparison.OrdinalIgnoreCase) &&
            !target.StartsWith(Path.TrimEndingDirectorySeparator(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("工作目录不能超出授权目录。");
        return AgentWorkspaceWriter.ValidateRoot(target);
    }

    private async Task<string> ExecuteAsync(string role, string command, string workingDirectory, string reason,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            if (command.Contains('\0')) throw new ArgumentException("命令不能包含空字符。");
            string directory = ResolveDirectory(workingDirectory);
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            AgentCommandApproval request = new(role, command, directory, reason, executable);
            progress.Report($"{role} 请求执行命令，等待确认：{command}");
            if (!await approve(request, cancellationToken).ConfigureAwait(false))
            {
                string denied = JsonSerializer.Serialize(new { status = "denied", command, directory, message = "用户拒绝执行；不得自行重试或绕过。" });
                lock (history) history.Add(denied);
                progress.Report("用户拒绝命令，未启动进程。");
                return denied;
            }
            cancellationToken.ThrowIfCancellationRequested();
            ResolveDirectory(workingDirectory);
            string script = "$ErrorActionPreference = 'Stop'\n$ProgressPreference = 'SilentlyContinue'\n" +
                "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)\n$OutputEncoding = [Console]::OutputEncoding\n" +
                "$global:LASTEXITCODE = 0\ntry {\n& {\n" + command +
                "\n}\nif (-not $?) { exit 1 }\nexit $global:LASTEXITCODE\n} catch {\n[Console]::Error.WriteLine($_.ToString())\nexit 1\n}";
            ProcessStartInfo startInfo = new(executable)
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) startInfo.ArgumentList.Add(argument);
            using Process process = new() { StartInfo = startInfo };
            using CancellationTokenSource outputCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            StringBuilder stdout = new();
            StringBuilder stderr = new();
            bool started = false;
            string state = "start_failed";
            int? exitCode = null;
            string error = "";
            Task outputTask = Task.CompletedTask;
            Task errorTask = Task.CompletedTask;
            try
            {
                started = process.Start();
                if (!started) throw new InvalidOperationException("无法启动命令解释器。");
                state = "running";
                process.StandardInput.Close();
                progress.Report($"开始执行 [{directory}] {command}");
                outputTask = PumpAsync(process.StandardOutput, stdout, "stdout", outputCancellation.Token);
                errorTask = PumpAsync(process.StandardError, stderr, "stderr", outputCancellation.Token);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                exitCode = process.ExitCode;
                state = exitCode == 0 ? "completed" : "failed";
            }
            catch (OperationCanceledException)
            {
                state = "cancelled";
                throw;
            }
            catch (Exception exception) when (exception is IOException or Win32Exception or InvalidOperationException)
            {
                error = exception.Message;
                state = state == "running" ? "failed" : state;
            }
            finally
            {
                if (started && !process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                    {
                        error += " 无法停止进程树，请人工检查：" + exception.Message;
                    }
                }
                outputCancellation.Cancel();
                try { await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false); }
                catch (Exception exception) when (exception is OperationCanceledException or IOException) { }
                string result = JsonSerializer.Serialize(new { status = state, command, directory, exitCode,
                    stdout = stdout.ToString(), stderr = stderr.ToString(), error });
                lock (history) history.Add(result);
                progress.Report($"命令结束：{state}；退出码：{exitCode?.ToString() ?? "无"}。{error}");
            }
            lock (history) return history[^1];
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            progress.Report("命令未执行：" + exception.Message);
            return JsonSerializer.Serialize(new { status = "invalid_request", error = exception.Message });
        }
        finally { gate.Release(); }
    }

    private async Task PumpAsync(StreamReader reader, StringBuilder output, string channel, CancellationToken cancellationToken)
    {
        char[] buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            string text = new(buffer, 0, count);
            output.Append(text);
            progress.Report($"[{channel}] {text}");
        }
    }
}
