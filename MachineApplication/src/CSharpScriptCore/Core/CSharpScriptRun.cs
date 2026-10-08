using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CSharpScriptCore.Models;
using CSharpScriptCore.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpScriptCore.Core;

/// <summary>
/// 执行脚本中声明的公共静态方法。
/// </summary>
public static class CSharpScriptRun
{
    /// <summary>
    /// 执行脚本中指定的公共静态方法，并返回原始脚本结果。
    /// </summary>
    public static ScriptResult RunStaticMethod(
        string code,
        string className,
        string methodName,
        params object[] parameters)
        => RunStaticMethodCore(code, null, className, methodName, parameters);

    /// <summary>
    /// 执行脚本中指定的公共静态方法，并将返回值转换为 <typeparamref name="T"/>。
    /// </summary>
    public static ScriptResult<T> RunStaticMethod<T>(
        string code,
        string className,
        string methodName,
        params object[] parameters)
        => RunStaticMethodCore<T>(code, null, className, methodName, parameters);

    /// <summary>异步执行代码中声明的公共静态方法。</summary>
    public static Task<ScriptResult> RunStaticMethodAsync(
        string code,
        string className,
        string methodName,
        ScriptExecutionOptions? options = null,
        object[]? parameters = null,
        CancellationToken cancellationToken = default) =>
        RunStaticMethodCoreAsync(code, null, className, methodName, options, parameters, cancellationToken);

    /// <summary>异步执行代码中声明的公共静态方法并转换返回值。</summary>
    public static Task<ScriptResult<T>> RunStaticMethodAsync<T>(
        string code,
        string className,
        string methodName,
        ScriptExecutionOptions? options = null,
        object[]? parameters = null,
        CancellationToken cancellationToken = default) =>
        RunStaticMethodCoreAsync<T>(code, null, className, methodName, options, parameters, cancellationToken);

    /// <summary>
    /// 读取指定 CSX 文件，并执行其中声明的公共静态方法。
    /// 相对 <c>#load</c> 路径以该 CSX 文件所在目录为基准。
    /// </summary>
    public static ScriptResult RunStaticMethodFromFile(
        string filePath,
        string className,
        string methodName,
        params object[] parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        string fullPath;
        string code;
        try
        {
            fullPath = Path.GetFullPath(filePath);
            code = File.ReadAllText(fullPath);
        }
        catch (Exception exception) when (IsFileLoadException(exception))
        {
            return FileLoadFailure(exception, stopwatch.ElapsedMilliseconds);
        }

        return RunStaticMethodCore(code, fullPath, className, methodName, parameters);
    }

    /// <summary>
    /// <see cref="RunStaticMethodFromFile(string,string,string,object[])"/> 的兼容命名入口。
    /// </summary>
    public static ScriptResult RunStaticMethodByFile(
        string filePath,
        string className,
        string methodName,
        params object[] parameters) =>
        RunStaticMethodFromFile(filePath, className, methodName, parameters);

    /// <summary>
    /// 读取指定 CSX 文件，并执行其中声明的公共静态方法，将返回值转换为 <typeparamref name="T"/>。
    /// 相对 <c>#load</c> 路径以该 CSX 文件所在目录为基准。
    /// </summary>
    public static ScriptResult<T> RunStaticMethodFromFile<T>(
        string filePath,
        string className,
        string methodName,
        params object[] parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        string fullPath;
        string code;
        try
        {
            fullPath = Path.GetFullPath(filePath);
            code = File.ReadAllText(fullPath);
        }
        catch (Exception exception) when (IsFileLoadException(exception))
        {
            return FileLoadFailure<T>(exception, stopwatch.ElapsedMilliseconds);
        }

        return RunStaticMethodCore<T>(code, fullPath, className, methodName, parameters);
    }

    /// <summary>
    /// <see cref="RunStaticMethodFromFile{T}(string,string,string,object[])"/> 的兼容命名入口。
    /// </summary>
    public static ScriptResult<T> RunStaticMethodByFile<T>(
        string filePath,
        string className,
        string methodName,
        params object[] parameters) =>
        RunStaticMethodFromFile<T>(filePath, className, methodName, parameters);

    /// <summary>异步读取 CSX 文件并执行其中声明的公共静态方法。</summary>
    public static async Task<ScriptResult> RunStaticMethodFromFileAsync(
        string filePath,
        string className,
        string methodName,
        ScriptExecutionOptions? options = null,
        object[]? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        return loaded.Failure ?? await RunStaticMethodCoreAsync(
            loaded.Code!, loaded.FullPath, className, methodName, options, parameters, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>异步读取 CSX 文件并执行静态方法，将返回值转换为 <typeparamref name="T"/>。</summary>
    public static async Task<ScriptResult<T>> RunStaticMethodFromFileAsync<T>(
        string filePath,
        string className,
        string methodName,
        ScriptExecutionOptions? options = null,
        object[]? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (loaded.Failure is not null)
        {
            return CopyFailure<T>(loaded.Failure);
        }

        return await RunStaticMethodCoreAsync<T>(
            loaded.Code!, loaded.FullPath, className, methodName, options, parameters, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ScriptResult RunStaticMethodCore(
        string code,
        string? scriptPath,
        string className,
        string methodName,
        object[]? parameters)
    {
        var invocationCode = CreateInvocationCode(code, className);
        var options = CreateOptions(scriptPath, methodName, parameters);
        return CSharpScript.ExecuteByCode(invocationCode, options);
    }

    private static ScriptResult<T> RunStaticMethodCore<T>(
        string code,
        string? scriptPath,
        string className,
        string methodName,
        object[]? parameters)
    {
        var invocationCode = CreateInvocationCode(code, className);
        var options = CreateOptions(scriptPath, methodName, parameters);
        return CSharpScript.ExecuteByCode<T>(invocationCode, options);
    }

    private static Task<ScriptResult> RunStaticMethodCoreAsync(
        string code,
        string? scriptPath,
        string className,
        string methodName,
        ScriptExecutionOptions? options,
        object[]? parameters,
        CancellationToken cancellationToken)
    {
        var invocationCode = CreateInvocationCode(code, className);
        options = PrepareOptions(options, scriptPath, methodName, parameters);
        return CSharpScript.ExecuteCodeAsync(invocationCode, options, cancellationToken);
    }

    private static Task<ScriptResult<T>> RunStaticMethodCoreAsync<T>(
        string code,
        string? scriptPath,
        string className,
        string methodName,
        ScriptExecutionOptions? options,
        object[]? parameters,
        CancellationToken cancellationToken)
    {
        var invocationCode = CreateInvocationCode(code, className);
        options = PrepareOptions(options, scriptPath, methodName, parameters);
        return CSharpScript.ExecuteCodeAsync<T>(invocationCode, options, cancellationToken);
    }

    private static ScriptExecutionOptions CreateOptions(
        string? scriptPath,
        string methodName,
        object[]? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var options = CSharpScript.CreateOptions(scriptPath);
        options.Globals = new StaticMethodGlobals(methodName, parameters ?? []);
        return options;
    }

    private static ScriptExecutionOptions PrepareOptions(
        ScriptExecutionOptions? options,
        string? scriptPath,
        string methodName,
        object[]? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        options ??= CSharpScript.CreateOptions(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            options.ScriptPath = Path.GetFullPath(scriptPath);
            options.BaseDirectory = Path.GetDirectoryName(options.ScriptPath) ?? options.BaseDirectory;
        }
        options.Globals = new StaticMethodGlobals(methodName, parameters ?? []);
        return options;
    }

    private static async Task<(string? Code, string? FullPath, ScriptResult? Failure)> LoadFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var code = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return (code, fullPath, null);
        }
        catch (OperationCanceledException exception)
        {
            return (null, null, new ScriptResult
            {
                RunResult = ScriptRunStatus.Cancelled,
                Exception = exception,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Output = $"{exception.GetType().Name}: {exception.Message}"
            });
        }
        catch (Exception exception) when (IsFileLoadException(exception))
        {
            return (null, null, FileLoadFailure(exception, stopwatch.ElapsedMilliseconds));
        }
    }

    private static ScriptResult<T> CopyFailure<T>(ScriptResult result) => new()
    {
        RunResult = result.RunResult,
        Exception = result.Exception,
        ElapsedMilliseconds = result.ElapsedMilliseconds,
        Diagnostics = result.Diagnostics,
        StructuredDiagnostics = result.StructuredDiagnostics,
        Output = result.Output,
        Dumps = result.Dumps,
        Timings = result.Timings
    };

    private static bool IsFileLoadException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static ScriptResult FileLoadFailure(Exception exception, long elapsedMilliseconds) => new()
    {
        RunResult = ScriptRunStatus.FileLoadError,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}"
    };

    private static ScriptResult<T> FileLoadFailure<T>(Exception exception, long elapsedMilliseconds) => new()
    {
        RunResult = ScriptRunStatus.FileLoadError,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}"
    };

    private static string CreateInvocationCode(string code, string className)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        var normalizedClassName = className.Trim();
        const string globalAlias = "global::";
        if (normalizedClassName.StartsWith(globalAlias, StringComparison.Ordinal))
        {
            normalizedClassName = normalizedClassName[globalAlias.Length..];
        }

        var typeName = SyntaxFactory.ParseName(normalizedClassName);
        if (typeName.ContainsDiagnostics || !IsSupportedTypeName(typeName))
        {
            throw new ArgumentException($"'{className}' is not a valid class name.", nameof(className));
        }

        var invocation =
            $"await __CSharpScriptRunInvokeAsync(typeof({typeName.WithoutTrivia()}))";
        return string.Concat(code, Environment.NewLine, invocation);
    }

    private static bool IsSupportedTypeName(NameSyntax name) => name switch
    {
        IdentifierNameSyntax => true,
        GenericNameSyntax genericName => genericName.TypeArgumentList.Arguments.All(
            argument => !argument.ContainsDiagnostics),
        QualifiedNameSyntax qualifiedName =>
            IsSupportedTypeName(qualifiedName.Left) && IsSupportedTypeName(qualifiedName.Right),
        _ => false
    };

    /// <summary>
    /// 为 Roslyn 脚本提供静态方法调用所需的参数和调用入口。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StaticMethodGlobals(string methodName, object[] parameters)
    {
        private readonly string _methodName = methodName;
        private readonly object[] _parameters = parameters;

        /// <summary>调用目标类型上的公共静态方法。</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<object?> __CSharpScriptRunInvokeAsync(Type targetType)
        {
            object? returnValue;
            try
            {
                returnValue = ScriptMethodBinder.Invoke(
                    targetType, null, _methodName, _parameters, ScriptExecution.CancellationToken);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            return await UnwrapAsyncReturnValue(returnValue).ConfigureAwait(false);
        }

        private static async Task<object?> UnwrapAsyncReturnValue(object? returnValue)
        {
            if (returnValue is Task task)
            {
                await task.ConfigureAwait(false);
                return task.GetType().IsGenericType
                    ? task.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(task)
                    : null;
            }

            if (returnValue is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
                return null;
            }

            if (returnValue is not null)
            {
                var returnType = returnValue.GetType();
                if (returnType.IsGenericType &&
                    returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    var valueTaskAsTask = (Task)returnType.GetMethod(nameof(ValueTask<int>.AsTask))!
                        .Invoke(returnValue, null)!;
                    await valueTaskAsTask.ConfigureAwait(false);
                    return valueTaskAsTask.GetType().GetProperty(nameof(Task<object>.Result))?
                        .GetValue(valueTaskAsTask);
                }
            }

            return returnValue;
        }
    }
}
