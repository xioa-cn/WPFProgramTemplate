using System.Collections;
using System.Data;
using System.Reflection;
using CSharpScriptCore.Models;

namespace CsxPad.Wpf.Services;

internal static class ScriptResultTable
{
    public static IReadOnlyList<ScriptResultTableData> CreateAll(ScriptResult result)
    {
        if (result.Dumps.Count == 0)
        {
            return [Create(new ScriptDump(result.ReturnValue, null), string.Empty)];
        }

        var showDefaultTitles = result.Dumps.Count > 1;
        return result.Dumps
            .Select((dump, index) => Create(
                dump,
                !string.IsNullOrWhiteSpace(dump.Title)
                    ? dump.Title!
                    : showDefaultTitles ? $"Dump {index + 1}" : string.Empty))
            .ToArray();
    }

    private static ScriptResultTableData Create(ScriptDump output, string title)
    {
        var isSequence = output.Value is IEnumerable and not string;
        var values = Materialize(output.Value);
        var table = new DataTable(output.Title ?? "Results");

        if (values.Count == 0)
        {
            table.Columns.Add("Result", typeof(object));
            return new ScriptResultTableData(title, table.DefaultView, 0, ScriptResultLayout.Empty);
        }

        var firstValue = values.FirstOrDefault(value => value is not null);
        if (firstValue is null || IsScalar(firstValue.GetType()))
        {
            AddScalarRows(table, values);
            return new ScriptResultTableData(
                title,
                table.DefaultView,
                table.Rows.Count,
                isSequence ? ScriptResultLayout.ScalarSequence : ScriptResultLayout.Scalar);
        }

        var members = GetReadableMembers(firstValue.GetType());
        if (members.Count == 0)
        {
            AddScalarRows(table, values.Select(FormatFallback));
            return new ScriptResultTableData(
                title,
                table.DefaultView,
                table.Rows.Count,
                isSequence ? ScriptResultLayout.ScalarSequence : ScriptResultLayout.Scalar);
        }

        if (!isSequence)
        {
            AddObjectMembers(table, firstValue, members);
            return new ScriptResultTableData(
                title,
                table.DefaultView,
                table.Rows.Count,
                ScriptResultLayout.Object);
        }

        AddObjectRows(table, values, members);
        return new ScriptResultTableData(
            title,
            table.DefaultView,
            table.Rows.Count,
            ScriptResultLayout.ObjectSequence);
    }

    internal static List<object?> Materialize(object? value)
    {
        if (value is null)
        {
            return [null];
        }

        return value is string || value is not IEnumerable sequence
            ? [value]
            : sequence.Cast<object?>().Take(1000).ToList();
    }

    private static void AddScalarRows(DataTable table, IEnumerable<object?> values)
    {
        table.Columns.Add("Value", typeof(object));
        foreach (var value in values)
        {
            table.Rows.Add(value ?? DBNull.Value);
        }
    }

    private static void AddObjectMembers(
        DataTable table,
        object value,
        IReadOnlyList<ResultMember> members)
    {
        table.Columns.Add("Member", typeof(string));
        table.Columns.Add("Value", typeof(object));
        foreach (var member in members)
        {
            table.Rows.Add(member.Name, ReadMemberValue(member, value));
        }
    }

    private static void AddObjectRows(
        DataTable table,
        IEnumerable<object?> values,
        IReadOnlyList<ResultMember> members)
    {
        foreach (var member in members)
        {
            table.Columns.Add(member.Name, typeof(object));
        }

        foreach (var value in values)
        {
            table.Rows.Add(members
                .Select(member => value is null ? DBNull.Value : ReadMemberValue(member, value))
                .ToArray());
        }
    }

    private static IReadOnlyList<ResultMember> GetReadableMembers(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => new ResultMember(
                property.Name,
                instance => property.GetValue(instance)));
        var fields = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => new ResultMember(
                field.Name,
                instance => field.GetValue(instance)));
        return properties
            .Concat(fields)
            .GroupBy(member => member.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static object ReadMemberValue(ResultMember member, object instance)
    {
        try
        {
            return member.GetValue(instance) ?? DBNull.Value;
        }
        catch (Exception exception)
        {
            return $"<{exception.GetType().Name}>";
        }
    }

    private static object FormatFallback(object? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        try
        {
            return value.ToString() ?? value.GetType().Name;
        }
        catch (Exception exception)
        {
            return $"<{exception.GetType().Name}>";
        }
    }

    private static bool IsScalar(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive || actualType.IsEnum || actualType == typeof(string) ||
               actualType == typeof(decimal) || actualType == typeof(DateTime) || actualType == typeof(DateOnly) ||
               actualType == typeof(TimeOnly) || actualType == typeof(Guid) || actualType == typeof(Uri) ||
               actualType == typeof(TimeSpan);
    }

    private sealed record ResultMember(string Name, Func<object, object?> GetValue);
}

internal sealed record ScriptResultTableData(
    string Title,
    DataView View,
    int RowCount,
    ScriptResultLayout Layout);
