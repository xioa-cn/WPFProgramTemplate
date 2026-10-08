using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CSharpScriptCore.Core;
using CSharpScriptCore.Models;
using CSharpScriptCore.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using RoslynCSharpScript = Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript;

namespace CSharpScriptCore;

/// <summary>
/// CSX 脚本的统一进程内执行入口。支持代码、文件、递归 #load、NuGet、编译缓存、取消和软超时。
/// 不可信脚本和硬超时应使用 <see cref="CSharpScriptWorker"/>。
/// </summary>
public static class CSharpScript
{
    private const int CompilationCacheLimit = 64;
    private static readonly ConcurrentDictionary<string, Script<object>> CompilationCache = new();
    private static readonly string[] DefaultImports =
    [
        "System", "System.Collections.Generic", "System.Linq", "System.Threading",
        "System.Threading.Tasks", "CSharpScriptCore.Runtime"
    ];

    /// <summary>同步执行代码。</summary>
    public static ScriptResult ExecuteByCode(string code) =>
        ExecuteCodeAsync(code).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>使用指定选项同步执行代码。</summary>
    public static ScriptResult ExecuteByCode(
        string code,
        ScriptExecutionOptions options,
        CancellationToken cancellationToken = default) =>
        ExecuteCodeAsync(code, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>同步执行代码并转换最后一个表达式。</summary>
    public static ScriptResult<T> ExecuteByCode<T>(string code) =>
        ExecuteCodeAsync<T>(code).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>使用指定选项同步执行代码并转换最后一个表达式。</summary>
    public static ScriptResult<T> ExecuteByCode<T>(
        string code,
        ScriptExecutionOptions options,
        CancellationToken cancellationToken = default) =>
        ExecuteCodeAsync<T>(code, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>同步执行 CSX 文件。</summary>
    public static ScriptResult ExecuteByFile(string filePath) =>
        ExecuteFileAsync(filePath).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>使用指定选项同步执行 CSX 文件。</summary>
    public static ScriptResult ExecuteByFile(
        string filePath,
        ScriptExecutionOptions options,
        CancellationToken cancellationToken = default) =>
        ExecuteFileAsync(filePath, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>同步执行 CSX 文件并转换最后一个表达式。</summary>
    public static ScriptResult<T> ExecuteByFile<T>(string filePath) =>
        ExecuteFileAsync<T>(filePath).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>使用指定选项同步执行 CSX 文件并转换最后一个表达式。</summary>
    public static ScriptResult<T> ExecuteByFile<T>(
        string filePath,
        ScriptExecutionOptions options,
        CancellationToken cancellationToken = default) =>
        ExecuteFileAsync<T>(filePath, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步执行代码。</summary>
    public static Task<ScriptResult> ExecuteCodeAsync(
        string code,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(code, options ?? new ScriptExecutionOptions(), false, cancellationToken);

    /// <summary>异步执行代码并转换最后一个表达式。</summary>
    public static async Task<ScriptResult<T>> ExecuteCodeAsync<T>(
        string code,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ConvertResult<T>(await ExecuteCodeAsync(code, options, cancellationToken).ConfigureAwait(false));

    /// <summary>异步读取并执行 CSX 文件。</summary>
    public static async Task<ScriptResult> ExecuteFileAsync(
        string filePath,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var code = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            options ??= new ScriptExecutionOptions();
            options.ScriptPath = fullPath;
            options.BaseDirectory = Path.GetDirectoryName(fullPath) ?? options.BaseDirectory;
            return await ExecuteCoreAsync(code, options, false, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(ScriptRunStatus.Cancelled, exception, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (IsFileException(exception))
        {
            return Failure(ScriptRunStatus.FileLoadError, exception, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>异步执行 CSX 文件并转换最后一个表达式。</summary>
    public static async Task<ScriptResult<T>> ExecuteFileAsync<T>(
        string filePath,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ConvertResult<T>(await ExecuteFileAsync(filePath, options, cancellationToken).ConfigureAwait(false));

    /// <summary>准备依赖并编译代码，但不执行。</summary>
    public static Task<ScriptResult> CompileCodeAsync(
        string code,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(code, options ?? new ScriptExecutionOptions(), true, cancellationToken);

    /// <summary>读取并编译 CSX 文件，但不执行。</summary>
    public static async Task<ScriptResult> CompileFileAsync(
        string filePath,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var code = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            options ??= new ScriptExecutionOptions();
            options.ScriptPath = fullPath;
            options.BaseDirectory = Path.GetDirectoryName(fullPath) ?? options.BaseDirectory;
            return await ExecuteCoreAsync(code, options, true, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(ScriptRunStatus.Cancelled, exception, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (IsFileException(exception))
        {
            return Failure(ScriptRunStatus.FileLoadError, exception, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>创建与脚本文件关联的选项。</summary>
    public static ScriptExecutionOptions CreateOptions(string? scriptPath = null) => new()
    {
        ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : Path.GetFullPath(scriptPath),
        BaseDirectory = string.IsNullOrWhiteSpace(scriptPath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Environment.CurrentDirectory
    };

    /// <summary>判断代码是否声明指定 framework 指令。</summary>
    public static bool UsesFramework(string code, string frameworkName) =>
        ScriptDependencyResolver.UsesFramework(code, frameworkName);

    /// <summary>清空当前进程的编译缓存。</summary>
    public static void ClearCompilationCache() => CompilationCache.Clear();

    /// <summary>当前进程的编译缓存条目数。</summary>
    public static int CompilationCacheCount => CompilationCache.Count;

    private static async Task<ScriptResult> ExecuteCoreAsync(
        string code,
        ScriptExecutionOptions options,
        bool compileOnly,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        var total = Stopwatch.StartNew();
        using var timeoutSource = options.Timeout.HasValue
            ? new CancellationTokenSource(options.Timeout.Value)
            : null;
        using var linkedSource = timeoutSource is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var effectiveToken = linkedSource?.Token ?? cancellationToken;
        var dumps = new List<ScriptDump>();
        var dependencyTime = 0L;
        var compilationTime = 0L;
        var executionTime = 0L;

        try
        {
            var phase = Stopwatch.StartNew();
            var prepared = await new ScriptDependencyResolver(options)
                .PrepareAsync(code, effectiveToken).ConfigureAwait(false);
            dependencyTime = phase.ElapsedMilliseconds;

            phase.Restart();
            var scriptOptions = CreateRoslynOptions(options, prepared.ReferencePaths);
            var executableCode = prepared.Code;
            var script = GetOrCreateScript(executableCode, scriptOptions, options, prepared.ReferencePaths);
            var diagnostics = script.Compile(effectiveToken)
                .Where(item => item.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                .ToArray();
            if (TryCreateRefStructLocalScope(executableCode, diagnostics, out var scopedCode))
            {
                executableCode = scopedCode;
                script = GetOrCreateScript(executableCode, scriptOptions, options, prepared.ReferencePaths);
                diagnostics = script.Compile(effectiveToken)
                    .Where(item => item.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                    .ToArray();
            }
            compilationTime = phase.ElapsedMilliseconds;
            var texts = diagnostics.Select(item => item.ToString()).ToArray();
            var structured = diagnostics.Select(ToStructuredDiagnostic).ToArray();
            var timings = () => new ScriptExecutionTimings(
                dependencyTime, compilationTime, executionTime, total.ElapsedMilliseconds);

            if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
            {
                return new ScriptResult
                {
                    RunResult = diagnostics.Any(IsDependencyLoadDiagnostic)
                        ? ScriptRunStatus.DependencyLoadFailed
                        : ScriptRunStatus.CompileFailed,
                    ElapsedMilliseconds = total.ElapsedMilliseconds,
                    Diagnostics = texts,
                    StructuredDiagnostics = structured,
                    Output = JoinOutput(prepared.RestoreOutput, texts),
                    Timings = timings()
                };
            }

            if (compileOnly)
            {
                return new ScriptResult
                {
                    RunResult = ScriptRunStatus.ReadyToRun,
                    ElapsedMilliseconds = total.ElapsedMilliseconds,
                    Diagnostics = texts,
                    StructuredDiagnostics = structured,
                    Output = prepared.RestoreOutput,
                    Timings = timings()
                };
            }

            using var runtimeScope = ScriptRuntime.Begin(effectiveToken, dumps);
            phase.Restart();
            var state = await script.RunAsync(options.Globals, cancellationToken: effectiveToken)
                .ConfigureAwait(false);
            executionTime = phase.ElapsedMilliseconds;
            return new ScriptResult
            {
                RunResult = ScriptRunStatus.Success,
                ElapsedMilliseconds = total.ElapsedMilliseconds,
                Diagnostics = texts,
                StructuredDiagnostics = structured,
                Output = prepared.RestoreOutput,
                ReturnValue = state.ReturnValue,
                Dumps = dumps.ToArray(),
                Timings = timings()
            };
        }
        catch (ScriptDependencyException exception)
        {
            return Failure(ScriptRunStatus.DependencyLoadFailed, exception, total.ElapsedMilliseconds, dumps,
                new ScriptExecutionTimings(dependencyTime, compilationTime, executionTime, total.ElapsedMilliseconds));
        }
        catch (CompilationErrorException exception)
        {
            var diagnostics = exception.Diagnostics.ToArray();
            return new ScriptResult
            {
                RunResult = ScriptRunStatus.CompileFailed,
                Exception = exception,
                ElapsedMilliseconds = total.ElapsedMilliseconds,
                Diagnostics = diagnostics.Select(item => item.ToString()).ToArray(),
                StructuredDiagnostics = diagnostics.Select(ToStructuredDiagnostic).ToArray(),
                Output = string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString())),
                Dumps = dumps.ToArray(),
                Timings = new ScriptExecutionTimings(
                    dependencyTime, compilationTime, executionTime, total.ElapsedMilliseconds)
            };
        }
        catch (OperationCanceledException exception)
        {
            var status = timeoutSource?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested
                ? ScriptRunStatus.Timeout
                : ScriptRunStatus.Cancelled;
            return Failure(status, exception, total.ElapsedMilliseconds, dumps,
                new ScriptExecutionTimings(dependencyTime, compilationTime, executionTime, total.ElapsedMilliseconds));
        }
        catch (Exception exception)
        {
            return Failure(ScriptRunStatus.RuntimeException, exception, total.ElapsedMilliseconds, dumps,
                new ScriptExecutionTimings(dependencyTime, compilationTime, executionTime, total.ElapsedMilliseconds));
        }
    }

    private static ScriptOptions CreateRoslynOptions(
        ScriptExecutionOptions options,
        IEnumerable<string> dependencyReferences)
    {
        var references = options.References
            .Concat(dependencyReferences)
            .Concat([typeof(CSharpScript).Assembly.Location]);
        if (options.IncludeLoadedAssemblies)
        {
            references = references.Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => assembly.Location));
        }
        var baseDirectory = !string.IsNullOrWhiteSpace(options.ScriptPath)
            ? Path.GetDirectoryName(Path.GetFullPath(options.ScriptPath)) ?? options.BaseDirectory
            : Path.GetFullPath(options.BaseDirectory);
        var scriptOptions = ScriptOptions.Default
            .AddReferences(references.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            .AddImports(DefaultImports.Concat(options.Imports).Distinct(StringComparer.Ordinal))
            .WithSourceResolver(ScriptSourceResolver.Default.WithBaseDirectory(baseDirectory));
        return string.IsNullOrWhiteSpace(options.ScriptPath)
            ? scriptOptions
            : scriptOptions.WithFilePath(Path.GetFullPath(options.ScriptPath));
    }

    private static Script<object> GetOrCreateScript(
        string code,
        ScriptOptions scriptOptions,
        ScriptExecutionOptions options,
        IReadOnlyList<string> dependencyReferences)
    {
        if (!options.EnableCompilationCache)
        {
            return RoslynCSharpScript.Create(code, scriptOptions, options.Globals?.GetType());
        }
        var keyText = string.Join('\n', new[]
        {
            code,
            options.Globals?.GetType().AssemblyQualifiedName ?? string.Empty,
            options.ScriptPath ?? options.BaseDirectory,
            string.Join('|', options.References.Concat(dependencyReferences)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join('|', options.Imports.OrderBy(value => value, StringComparer.Ordinal))
        });
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyText)));
        var script = CompilationCache.GetOrAdd(key,
            _ => RoslynCSharpScript.Create(code, scriptOptions, options.Globals?.GetType()));
        if (CompilationCache.Count > CompilationCacheLimit)
        {
            foreach (var staleKey in CompilationCache.Keys.Take(CompilationCache.Count - CompilationCacheLimit))
            {
                CompilationCache.TryRemove(staleKey, out _);
            }
        }
        return script;
    }

    private static bool TryCreateRefStructLocalScope(
        string code,
        IReadOnlyList<Diagnostic> diagnostics,
        out string scopedCode)
    {
        scopedCode = code;
        var refStructDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Id == "CS8345" && diagnostic.Location.IsInSource)
            .ToArray();
        if (refStructDiagnostics.Length == 0)
        {
            return false;
        }

        var sourceTree = refStructDiagnostics
            .Select(diagnostic => diagnostic.Location.SourceTree)
            .FirstOrDefault(tree => tree is not null);
        if (sourceTree?.GetRoot() is not CompilationUnitSyntax root)
        {
            return false;
        }

        var targetIndexes = refStructDiagnostics
            .Where(diagnostic => ReferenceEquals(diagnostic.Location.SourceTree, sourceTree))
            .Select(diagnostic => FindContainingGlobalStatement(root, diagnostic.Location.SourceSpan))
            .Where(global => global is not null)
            .Select(global => root.Members.IndexOf(global!))
            .Where(index => index >= 0)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();
        if (targetIndexes.Length == 0)
        {
            return false;
        }

        var firstTargetIndex = targetIndexes.Min();
        var statements = root.Members
            .Skip(firstTargetIndex)
            .OfType<GlobalStatementSyntax>()
            .Select(global => global.Statement)
            .ToArray();
        if (statements.Length == 0)
        {
            return false;
        }

        var methodName = CreateUniqueGeneratedMethodName(root);
        var isAsync = statements.Any(statement => statement.DescendantNodesAndSelf()
            .OfType<AwaitExpressionSyntax>()
            .Any());
        var returnType = isAsync
            ? SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task")
            : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        var method = SyntaxFactory.MethodDeclaration(returnType, methodName)
            .WithModifiers(isAsync
                ? SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
                : default)
            .WithBody(SyntaxFactory.Block(statements)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        var invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName));
        StatementSyntax invocationStatement = isAsync
            ? SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(invocation))
            : SyntaxFactory.ExpressionStatement(invocation);
        invocationStatement = invocationStatement
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        var rewrittenMembers = new List<MemberDeclarationSyntax>();
        for (var index = 0; index < root.Members.Count; index++)
        {
            if (index == firstTargetIndex)
            {
                rewrittenMembers.Add(SyntaxFactory.GlobalStatement(invocationStatement));
            }

            var member = root.Members[index];
            if (index >= firstTargetIndex && member is GlobalStatementSyntax)
            {
                continue;
            }

            rewrittenMembers.Add(member);
        }

        rewrittenMembers.Add(method);
        scopedCode = root.WithMembers(SyntaxFactory.List(rewrittenMembers)).ToFullString();
        return true;
    }

    private static string CreateUniqueGeneratedMethodName(CompilationUnitSyntax root)
    {
        const string baseName = "__CsxPadExecuteRefStructScope";
        var identifiers = root.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        if (!identifiers.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (identifiers.Contains($"{baseName}{suffix}"))
        {
            suffix++;
        }

        return $"{baseName}{suffix}";
    }

    private static GlobalStatementSyntax? FindContainingGlobalStatement(
        CompilationUnitSyntax root,
        TextSpan diagnosticSpan)
    {
        var boundedStart = Math.Clamp(diagnosticSpan.Start, 0, root.FullSpan.End);
        var boundedLength = Math.Clamp(
            diagnosticSpan.Length,
            0,
            root.FullSpan.End - boundedStart);
        var node = root.FindNode(
            new TextSpan(boundedStart, boundedLength),
            getInnermostNodeForTie: true);
        return node.AncestorsAndSelf()
            .OfType<GlobalStatementSyntax>()
            .FirstOrDefault();
    }

    private static ScriptDiagnostic ToStructuredDiagnostic(Diagnostic diagnostic)
    {
        var hasLocation = diagnostic.Location.IsInSource;
        var span = diagnostic.Location.GetLineSpan();
        return new ScriptDiagnostic(
            diagnostic.Id,
            diagnostic.Severity.ToString(),
            diagnostic.GetMessage(),
            hasLocation ? span.Path : null,
            hasLocation ? span.StartLinePosition.Line + 1 : null,
            hasLocation ? span.StartLinePosition.Character + 1 : null,
            hasLocation ? span.EndLinePosition.Line + 1 : null,
            hasLocation ? span.EndLinePosition.Character + 1 : null);
    }

    private static ScriptResult<T> ConvertResult<T>(ScriptResult result)
    {
        if (!result.Success || result.ReturnValue is null || result.ReturnValue is T)
        {
            return CopyResult(result, result.ReturnValue is T value ? value : default);
        }
        var exception = new InvalidCastException(
            $"Script returned '{result.ReturnValue.GetType().FullName}', which cannot be converted to '{typeof(T).FullName}'.");
        return new ScriptResult<T>
        {
            RunResult = ScriptRunStatus.RuntimeException,
            Exception = exception,
            ElapsedMilliseconds = result.ElapsedMilliseconds,
            Diagnostics = result.Diagnostics,
            StructuredDiagnostics = result.StructuredDiagnostics,
            Output = exception.Message,
            ReturnValue = result.ReturnValue,
            Dumps = result.Dumps,
            Timings = result.Timings
        };
    }

    private static ScriptResult<T> CopyResult<T>(ScriptResult result, T? data) => new()
    {
        RunResult = result.RunResult,
        Exception = result.Exception,
        ElapsedMilliseconds = result.ElapsedMilliseconds,
        Diagnostics = result.Diagnostics,
        StructuredDiagnostics = result.StructuredDiagnostics,
        Output = result.Output,
        ReturnValue = result.ReturnValue,
        Dumps = result.Dumps,
        Timings = result.Timings,
        Data = data
    };

    private static ScriptResult Failure(
        ScriptRunStatus status,
        Exception exception,
        long elapsedMilliseconds,
        IReadOnlyList<ScriptDump>? dumps = null,
        ScriptExecutionTimings? timings = null) => new()
    {
        RunResult = status,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}",
        Dumps = dumps ?? [],
        Timings = timings
    };

    private static bool IsFileException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string JoinOutput(string restoreOutput, IReadOnlyList<string> diagnostics) =>
        string.Join(Environment.NewLine, new[] { restoreOutput }.Concat(diagnostics)
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static bool IsDependencyLoadDiagnostic(Diagnostic diagnostic) =>
        diagnostic.Id is "CS1504" or "CS2001" ||
        diagnostic.GetMessage().Contains("#load", StringComparison.OrdinalIgnoreCase);
}
