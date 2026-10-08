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
/// 创建脚本中声明的类实例，并调用该实例上的公开方法。
/// </summary>
public static class CSharpScriptService
{
    /// <summary>列出已创建脚本实例上的公开实例方法签名。</summary>
    public static IReadOnlyList<ScriptMethodDescriptor> GetPublicMethods<T>(this ScriptClass<T> scriptClass)
    {
        ArgumentNullException.ThrowIfNull(scriptClass);
        if (!scriptClass.Success || scriptClass.Data is null) return [];
        return scriptClass.Data.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(ToDescriptor)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static ScriptMethodDescriptor ToDescriptor(MethodInfo method) => new(
        method.Name,
        method.ReturnType.FullName ?? method.ReturnType.Name,
        method.IsStatic,
        method.IsGenericMethodDefinition,
        method.GetParameters().Select(parameter => new ScriptParameterDescriptor(
            parameter.Name ?? string.Empty,
            parameter.ParameterType.FullName ?? parameter.ParameterType.Name,
            parameter.IsOptional,
            parameter.GetCustomAttribute<ParamArrayAttribute>() is not null,
            parameter.IsOut,
            parameter.ParameterType.IsByRef)).ToArray());

    /// <summary>编译脚本并使用指定构造参数创建类实例。</summary>
    public static ScriptClass<T> GetServiceFromClass<T>(
        string code,
        string className,
        params object[] args)
        => GetServiceFromClassCore<T>(code, null, className, args);

    /// <summary>异步编译脚本并创建类实例。</summary>
    public static Task<ScriptClass<T>> GetServiceFromClassAsync<T>(
        string code,
        string className,
        ScriptExecutionOptions? options = null,
        object[]? args = null,
        CancellationToken cancellationToken = default) =>
        GetServiceFromClassCoreAsync<T>(code, null, className, options, args, cancellationToken);

    /// <summary>
    /// 读取指定 CSX 文件，并创建其中声明的类实例。
    /// 相对 <c>#load</c> 路径以该 CSX 文件所在目录为基准。
    /// </summary>
    public static ScriptClass<T> GetServiceFromFile<T>(
        string filePath,
        string className,
        params object[] args)
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

        return GetServiceFromClassCore<T>(code, fullPath, className, args);
    }

    /// <summary>
    /// <see cref="GetServiceFromFile{T}(string,string,object[])"/> 的兼容命名入口。
    /// </summary>
    public static ScriptClass<T> GetServiceFromClassByFile<T>(
        string filePath,
        string className,
        params object[] args) =>
        GetServiceFromFile<T>(filePath, className, args);

    /// <summary>异步读取 CSX 文件并创建类实例。</summary>
    public static async Task<ScriptClass<T>> GetServiceFromFileAsync<T>(
        string filePath,
        string className,
        ScriptExecutionOptions? options = null,
        object[]? args = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var code = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return await GetServiceFromClassCoreAsync<T>(
                code, fullPath, className, options, args, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return FailureClass<T>(ScriptRunStatus.Cancelled, exception, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (IsFileLoadException(exception))
        {
            return FileLoadFailure<T>(exception, stopwatch.ElapsedMilliseconds);
        }
    }

    private static ScriptClass<T> GetServiceFromClassCore<T>(
        string code,
        string? scriptPath,
        string className,
        object[]? args)
    {
        var executableCode = CreateInstanceCode(code, className);
        var options = CSharpScript.CreateOptions(scriptPath);
        options.Globals = new ScriptClassFactoryGlobals(args ?? []);
        var result = CSharpScript.ExecuteByCode<T>(executableCode, options);
        return CopyClassResult(result);
    }

    private static async Task<ScriptClass<T>> GetServiceFromClassCoreAsync<T>(
        string code,
        string? scriptPath,
        string className,
        ScriptExecutionOptions? options,
        object[]? args,
        CancellationToken cancellationToken)
    {
        var executableCode = CreateInstanceCode(code, className);
        options ??= CSharpScript.CreateOptions(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            options.ScriptPath = Path.GetFullPath(scriptPath);
            options.BaseDirectory = Path.GetDirectoryName(options.ScriptPath) ?? options.BaseDirectory;
        }
        options.Globals = new ScriptClassFactoryGlobals(args ?? []);
        var result = await CSharpScript.ExecuteCodeAsync<T>(executableCode, options, cancellationToken)
            .ConfigureAwait(false);
        return CopyClassResult(result);
    }

    private static bool IsFileLoadException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static ScriptClass<T> FileLoadFailure<T>(Exception exception, long elapsedMilliseconds) => new()
    {
        RunResult = ScriptRunStatus.FileLoadError,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}"
    };

    /// <summary>调用已创建脚本实例上的公开实例方法。</summary>
    public static ScriptResult<TMethodResult> ScriptClassExecuteMethod<T, TMethodResult>(
        this ScriptClass<T> scriptClass,
        string methodName,
        params object[] args)
    {
        ArgumentNullException.ThrowIfNull(scriptClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        if (!scriptClass.Success)
        {
            return CopyMethodResult<TMethodResult>(scriptClass);
        }

        if (scriptClass.Data is null)
        {
            return Failure<TMethodResult>(new InvalidOperationException(
                "The script class instance is not available."));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var returnValue = InvokeMethodAsync(scriptClass.Data, methodName, args ?? [], CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return ConvertSuccess<TMethodResult>(returnValue, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            return Failure<TMethodResult>(exception, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>异步调用脚本实例上的公开方法。</summary>
    public static async Task<ScriptResult<TMethodResult>> ScriptClassExecuteMethodAsync<T, TMethodResult>(
        this ScriptClass<T> scriptClass,
        string methodName,
        object[]? args = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scriptClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        if (!scriptClass.Success) return CopyMethodResult<TMethodResult>(scriptClass);
        if (scriptClass.Data is null)
        {
            return Failure<TMethodResult>(new InvalidOperationException(
                "The script class instance is not available."));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dumps = new List<ScriptDump>();
            using var runtimeScope = ScriptRuntime.Begin(cancellationToken, dumps);
            var returnValue = await InvokeMethodAsync(scriptClass.Data, methodName, args ?? [], cancellationToken)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return CopyWithDumps(
                ConvertSuccess<TMethodResult>(returnValue, stopwatch.ElapsedMilliseconds), dumps);
        }
        catch (OperationCanceledException exception)
        {
            return new ScriptResult<TMethodResult>
            {
                RunResult = ScriptRunStatus.Cancelled,
                Exception = exception,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Output = $"{exception.GetType().Name}: {exception.Message}"
            };
        }
        catch (Exception exception)
        {
            return Failure<TMethodResult>(exception, stopwatch.ElapsedMilliseconds);
        }
    }

    private static string CreateInstanceCode(string code, string className)
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

        var createExpression = $"__CSharpScriptServiceCreate(typeof({typeName.WithoutTrivia()}))";
        return string.Concat(code, Environment.NewLine, createExpression);
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

    private static async Task<object?> InvokeMethodAsync(
        object instance,
        string methodName,
        object[] args,
        CancellationToken cancellationToken)
    {
        object? returnValue;
        try
        {
            returnValue = ScriptMethodBinder.Invoke(
                instance.GetType(), instance, methodName, args, cancellationToken);
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
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
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

    private static ScriptClass<T> CopyClassResult<T>(ScriptResult<T> result) => new()
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
        Data = result.Data
    };

    private static ScriptResult<TMethodResult> CopyMethodResult<TMethodResult>(ScriptResult result) => new()
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

    private static ScriptClass<T> FailureClass<T>(
        ScriptRunStatus status,
        Exception exception,
        long elapsedMilliseconds) => new()
    {
        RunResult = status,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}"
    };

    private static ScriptResult<TMethodResult> ConvertSuccess<TMethodResult>(
        object? returnValue,
        long elapsedMilliseconds)
    {
        if (returnValue is null || returnValue is TMethodResult)
        {
            return new ScriptResult<TMethodResult>
            {
                RunResult = ScriptRunStatus.Success,
                ElapsedMilliseconds = elapsedMilliseconds,
                ReturnValue = returnValue,
                Data = returnValue is TMethodResult value ? value : default
            };
        }

        return Failure<TMethodResult>(new InvalidCastException(
            $"Method returned '{returnValue.GetType().FullName}', which cannot be converted to " +
            $"'{typeof(TMethodResult).FullName}'."), elapsedMilliseconds, returnValue);
    }

    private static ScriptResult<TMethodResult> Failure<TMethodResult>(
        Exception exception,
        long elapsedMilliseconds = 0,
        object? returnValue = null) => new()
    {
        RunResult = ScriptRunStatus.RuntimeException,
        Exception = exception,
        ElapsedMilliseconds = elapsedMilliseconds,
        Output = $"{exception.GetType().Name}: {exception.Message}",
        ReturnValue = returnValue
    };

    private static ScriptResult<TMethodResult> CopyWithDumps<TMethodResult>(
        ScriptResult<TMethodResult> result,
        IReadOnlyList<ScriptDump> dumps) => new()
    {
        RunResult = result.RunResult,
        Exception = result.Exception,
        ElapsedMilliseconds = result.ElapsedMilliseconds,
        Diagnostics = result.Diagnostics,
        StructuredDiagnostics = result.StructuredDiagnostics,
        Output = result.Output,
        ReturnValue = result.ReturnValue,
        Dumps = dumps,
        Timings = result.Timings,
        Data = result.Data
    };

    /// <summary>为 Roslyn 脚本提供构造参数和实例创建入口。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ScriptClassFactoryGlobals(object[] args)
    {
        private readonly object[] _args = args;

        /// <summary>创建指定的脚本类型。</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public object __CSharpScriptServiceCreate(Type targetType)
        {
            try
            {
                return ScriptMethodBinder.CreateInstance(targetType, _args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
