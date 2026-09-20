using System.Diagnostics;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace MachineApplication.Create;

internal static class SolutionService
{
    public static string Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var paths = dir.GetFiles("*.slnx").Concat(dir.GetFiles("*.sln")).ToArray();
                if (paths.Length == 1) return paths[0].FullName;
                if (paths.Length > 1) throw new InvalidOperationException("目录存在多个解决方案，请保留一个目标解决方案后运行。");
            }
        throw new FileNotFoundException("未找到 .sln/.slnx 解决方案。");
    }

    public static IEnumerable<string> Folders(string solution)
    {
        if (Path.GetExtension(solution).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            return XDocument.Load(solution).Descendants("Folder").Select(e => (string?)e.Attribute("Name") ?? "");
        return Regex.Matches(File.ReadAllText(solution),
            "Project\\(\"\\{2150E333-8FDC-42A3-9474-1A3956D46DE8\\}\"\\) = \"([^\"]+)\"",
            RegexOptions.IgnoreCase).Select(m => m.Groups[1].Value);
    }

    public static async Task AddAsync(string solution, string project, string folder)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "sln", solution, "add", project }) info.ArgumentList.Add(arg);
        folder = folder.Trim('/', '\\');
        if (folder.Length == 0) info.ArgumentList.Add("--in-root");
        else
        {
            info.ArgumentList.Add("--solution-folder");
            info.ArgumentList.Add(folder);
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动 dotnet。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var details = (await output) + (await error);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"项目文件已生成，但添加到解决方案失败（退出码 {process.ExitCode}）：{project}{Environment.NewLine}{details}");
    }
}


