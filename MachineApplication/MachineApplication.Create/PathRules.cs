namespace MachineApplication.Create;

internal static class PathRules
{
    public static void Validate(string root, string target)
    {
        root = Path.GetFullPath(root);
        target = Path.GetFullPath(target);
        var relative = Path.GetRelativePath(root, target);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new ArgumentException("生成目录必须位于解决方案目录下。");
        for (var d = new DirectoryInfo(target); d != null; d = d.Parent)
        {
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("生成路径不能经过符号链接或目录联接。");
            if (string.Equals(d.FullName, root, StringComparison.OrdinalIgnoreCase)) break;
        }
    }
}

