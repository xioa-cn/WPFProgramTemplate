namespace MachineApplication.Create;

public sealed class ModuleProjectGenerator
{
    public async Task<int> RunAsync()
    {
        try
        {
            ConsoleUi.Header();
            ConsoleUi.Step(1, "📂 选择解决方案与项目");
            var solution = SolutionService.Find();
            var root = Path.GetDirectoryName(solution)!;
            ConsoleUi.Info($"📄 解决方案: {solution}");
            var parent = ConsoleUi.Ask("生成路径（相对解决方案目录或绝对路径）", "src");
            var project = ConsoleUi.Ask("项目名称", "MachineApplication.Module");
            NameRules.Validate(project, true);
            ConsoleUi.Step(2, "🧩 配置模块与语言资源");
            var createModule = ConsoleUi.Ask("是否生成 Module 类及语言资源？Y/n", "Y").Equals("Y", StringComparison.OrdinalIgnoreCase);
            var module = string.Empty;
            if (createModule)
            {
                module = ConsoleUi.Ask("模块类名称（同时用于命名语言类）", "SampleModule");
                NameRules.Validate(module, false);
            }
            var loadDesignerStyles = ConsoleUi.Ask("是否加载设计器样式？Y/n", "Y").Equals("Y", StringComparison.OrdinalIgnoreCase);
            ConsoleUi.Info("📂 现有解决方案文件夹：" + string.Join(", ", SolutionService.Folders(solution)));
            var folder = ConsoleUi.Ask("解决方案文件夹（可输入已有或新名称，/ 表示根目录）", "/modules/");
            var dir = Path.GetFullPath(Path.Combine(root, parent, project));
            PathRules.Validate(root, dir);
            if (Directory.Exists(dir) || File.Exists(dir))
                throw new InvalidOperationException("项目目录已存在，请使用新项目名称。");
            var files = ProjectTemplates.Create(root, dir, project, module, createModule, loadDesignerStyles);
            ConsoleUi.Step(3, "🔨 生成项目");
            using (var progress = new GenerationProgress(files.Count + 1))
            {
                Directory.CreateDirectory(dir);
                foreach (var (name, content) in files)
                {
                    await progress.RunStepAsync("写入 " + name, async () =>
                    {
                        var path = Path.Combine(dir, name);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        await File.WriteAllTextAsync(path, content);
                    });
                }
                await progress.RunStepAsync("加入解决方案", () =>
                    SolutionService.AddAsync(solution, Path.Combine(dir, project + ".csproj"), folder));
            }
            ConsoleUi.Success(dir);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUi.Error(ex.Message);
            return 1;
        }
    }

}





