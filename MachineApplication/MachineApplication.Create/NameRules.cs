using System.Text.RegularExpressions;

namespace MachineApplication.Create;

internal static class NameRules
{
    private static readonly HashSet<string> Keywords = new(
        ("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while").Split(' '));

    public static void Validate(string name, bool qualified)
    {
        var parts = qualified ? name.Split('.') : new[] { name };
        if (parts.Any(p => !Regex.IsMatch(p, @"^[A-Za-z_][A-Za-z0-9_]*$") || Keywords.Contains(p))
            || parts.Any(p => Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)))
            throw new ArgumentException("名称必须是合法的 C# 标识符；项目名称支持以点分隔，不能使用关键字或设备名称。");
    }
}

