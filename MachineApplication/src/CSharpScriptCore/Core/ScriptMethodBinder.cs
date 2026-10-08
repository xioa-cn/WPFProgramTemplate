using System.Globalization;
using System.Reflection;

namespace CSharpScriptCore.Core;

internal static class ScriptMethodBinder
{
    public static object? Invoke(Type type, object? target, string name, object?[] supplied,
        CancellationToken cancellationToken = default)
    {
        var flags = BindingFlags.Public | (target is null ? BindingFlags.Static : BindingFlags.Instance);
        var matches = type.GetMethods(flags)
            .Where(method => method.Name == name)
            .Select(method => CloseGeneric(method, supplied))
            .Where(method => method is not null)
            .Select(method => TryBind(method!, supplied, cancellationToken))
            .Where(call => call is not null)
            .OrderBy(call => call!.Score).ToArray();
        var call = Select(matches, $"method '{type.FullName}.{name}'");
        return InvokeCore(call.Method, target, call.Arguments);
    }

    public static object CreateInstance(Type type, object?[] supplied)
    {
        var matches = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => TryBind(method, supplied, CancellationToken.None))
            .Where(call => call is not null)
            .OrderBy(call => call!.Score).ToArray();
        var call = Select(matches, $"constructor for '{type.FullName}'");
        return InvokeCore(call.Method, null, call.Arguments)
               ?? throw new InvalidOperationException($"Constructor for '{type.FullName}' returned no instance.");
    }

    private static BoundCall Select(BoundCall?[] matches, string description)
    {
        if (matches.Length == 0) throw new MissingMethodException($"No public {description} matches the supplied arguments.");
        if (matches.Length > 1 && matches[0]!.Score == matches[1]!.Score)
            throw new AmbiguousMatchException($"More than one public {description} matches the supplied arguments.");
        return matches[0]!;
    }

    private static object? InvokeCore(MethodBase method, object? target, object?[] arguments)
    {
        try
        {
            return method switch
            {
                MethodInfo info => info.Invoke(target, arguments),
                ConstructorInfo constructor => constructor.Invoke(arguments),
                _ => throw new NotSupportedException(method.GetType().FullName)
            };
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static MethodInfo? CloseGeneric(MethodInfo method, object?[] arguments)
    {
        if (!method.IsGenericMethodDefinition) return method;
        var genericParameters = method.GetGenericArguments();
        var inferred = new Type?[genericParameters.Length];
        var parameters = method.GetParameters();
        for (var index = 0; index < Math.Min(arguments.Length, parameters.Length); index++)
        {
            if (arguments[index] is not null)
                Infer(parameters[index].ParameterType, arguments[index]!.GetType(), genericParameters, inferred);
        }
        try
        {
            return inferred.All(type => type is not null)
                ? method.MakeGenericMethod(inferred.Select(type => type!).ToArray())
                : null;
        }
        catch (ArgumentException) { return null; }
    }

    private static void Infer(Type parameter, Type actual, Type[] genericParameters, Type?[] inferred)
    {
        if (parameter.IsGenericParameter)
        {
            var index = Array.IndexOf(genericParameters, parameter);
            if (index >= 0) inferred[index] ??= actual;
            return;
        }
        if (!parameter.IsGenericType) return;
        var definition = parameter.GetGenericTypeDefinition();
        var match = actual.IsGenericType && actual.GetGenericTypeDefinition() == definition
            ? actual
            : actual.GetInterfaces().FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == definition);
        if (match is null) return;
        var expectedArguments = parameter.GetGenericArguments();
        var actualArguments = match.GetGenericArguments();
        for (var index = 0; index < expectedArguments.Length; index++)
            Infer(expectedArguments[index], actualArguments[index], genericParameters, inferred);
    }

    private static BoundCall? TryBind(MethodBase method, object?[] supplied, CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var sourceIndex = 0;
        var score = 0;
        for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
        {
            var parameter = parameters[parameterIndex];
            if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                var elementType = parameter.ParameterType.GetElementType()!;
                var array = Array.CreateInstance(elementType, supplied.Length - sourceIndex);
                for (var index = 0; sourceIndex < supplied.Length; index++, sourceIndex++)
                {
                    if (!TryConvert(supplied[sourceIndex], elementType, out var value, out var conversionScore)) return null;
                    array.SetValue(value, index);
                    score += conversionScore + 1;
                }
                arguments[parameterIndex] = array;
                continue;
            }
            if (sourceIndex < supplied.Length)
            {
                var targetType = parameter.ParameterType.IsByRef
                    ? parameter.ParameterType.GetElementType()!
                    : parameter.ParameterType;
                if (!TryConvert(supplied[sourceIndex++], targetType, out var value, out var conversionScore)) return null;
                arguments[parameterIndex] = value;
                score += conversionScore;
            }
            else if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments[parameterIndex] = cancellationToken;
                score++;
            }
            else if (parameter.IsOptional)
            {
                arguments[parameterIndex] = parameter.DefaultValue;
                score += 2;
            }
            else if (parameter.IsOut)
            {
                var elementType = parameter.ParameterType.GetElementType()!;
                arguments[parameterIndex] = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
                score += 2;
            }
            else return null;
        }
        return sourceIndex == supplied.Length ? new BoundCall(method, arguments, score) : null;
    }

    private static bool TryConvert(object? value, Type target, out object? converted, out int score)
    {
        var actualTarget = Nullable.GetUnderlyingType(target) ?? target;
        if (value is null)
        {
            converted = null; score = 1;
            return !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;
        }
        if (target.IsInstanceOfType(value))
        {
            converted = value; score = value.GetType() == target ? 0 : 1; return true;
        }
        try
        {
            if (actualTarget.IsEnum)
                converted = value is string text ? Enum.Parse(actualTarget, text, true) : Enum.ToObject(actualTarget, value);
            else if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(actualTarget))
                converted = Convert.ChangeType(value, actualTarget, CultureInfo.InvariantCulture);
            else { converted = null; score = int.MaxValue; return false; }
            score = 3; return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            converted = null; score = int.MaxValue; return false;
        }
    }

    private sealed record BoundCall(MethodBase Method, object?[] Arguments, int Score);
}
