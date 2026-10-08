using System.Windows;
using System.Windows.Threading;

namespace CsxPad.Wpf.Scripting;

public sealed class ScriptDebugSession(
    IEnumerable<int> breakpoints,
    CancellationToken executionCancellation = default)
{
    private readonly HashSet<int> _breakpoints = breakpoints.Where(line => line > 0).ToHashSet();
    private readonly object _syncRoot = new();
    private TaskCompletionSource? _resumeSource;
    private bool _stepRequested;
    private int? _resumedLine;

    public event Action<int?>? PausedLineChanged;

    public event Action<ScriptDebugPause?>? PausedChanged;

    public IReadOnlySet<int> Breakpoints => _breakpoints;

    public CancellationToken ExecutionCancellation { get; } = executionCancellation;

    public async Task PauseIfNeededAsync(
        int line,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (line == _resumedLine)
            {
                return;
            }

            if (!_breakpoints.Contains(line) && !_stepRequested)
            {
                return;
            }

            _stepRequested = false;
            _resumedLine = null;
        }

        TaskCompletionSource resumeSource;
        lock (_syncRoot)
        {
            _resumeSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            resumeSource = _resumeSource;
            CurrentPause = new ScriptDebugPause(line, values);
        }

        PausedLineChanged?.Invoke(line);
        PausedChanged?.Invoke(CurrentPause);
        try
        {
            await resumeSource.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_resumeSource, resumeSource))
                {
                    _resumeSource = null;
                    CurrentPause = null;
                }
            }

            PausedLineChanged?.Invoke(null);
            PausedChanged?.Invoke(null);
        }
    }

    public void Continue()
    {
        TaskCompletionSource? resumeSource;
        lock (_syncRoot)
        {
            _stepRequested = false;
            _resumedLine = null;
            resumeSource = _resumeSource;
        }

        resumeSource?.TrySetResult();
    }

    public void Step()
    {
        TaskCompletionSource? resumeSource;
        lock (_syncRoot)
        {
            _stepRequested = true;
            _resumedLine = CurrentPause?.Line;
            resumeSource = _resumeSource;
        }

        resumeSource?.TrySetResult();
    }

    public ScriptDebugPause? CurrentPause { get; private set; }
}

public sealed record ScriptDebugPause(int Line, IReadOnlyDictionary<string, object?> Values);

public static class ScriptDebugger
{
    private static readonly AsyncLocal<ScriptDebugSession?> CurrentSession = new();

    public static Task PauseAsync(
        int line,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default) =>
        CurrentSession.Value is { } session
            ? session.PauseIfNeededAsync(
                line,
                values,
                cancellationToken.CanBeCanceled ? cancellationToken : session.ExecutionCancellation)
            : Task.CompletedTask;

    public static void Pause(
        int line,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        var pauseTask = PauseAsync(line, values, cancellationToken);
        if (pauseTask.IsCompleted)
        {
            pauseTask.GetAwaiter().GetResult();
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || !dispatcher.CheckAccess())
        {
            pauseTask.ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }

        var frame = new DispatcherFrame();
        _ = pauseTask.ContinueWith(
            _ => dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() => frame.Continue = false)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        pauseTask.GetAwaiter().GetResult();
    }

    internal static IDisposable BeginSession(ScriptDebugSession session)
    {
        var previous = CurrentSession.Value;
        CurrentSession.Value = session;
        return new SessionScope(() => CurrentSession.Value = previous);
    }

    private sealed class SessionScope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
