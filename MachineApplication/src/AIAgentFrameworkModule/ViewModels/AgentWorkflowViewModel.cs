using System.Collections.ObjectModel;
using AIAgentFrameworkModule.Models;
using AIAgentFrameworkModule.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAgentFrameworkModule.ViewModels;

public sealed class AgentWorkflowRoleViewModel : ObservableObject
{
    private AgentModelProfile? profile;
    private string instructions;
    public string Id { get; }
    public string Name { get; }

    public string Number => Id switch { "analyst" => "01", "reviewer" => "02", _ => "03" };
    public string Description => Id switch { "analyst" => "拆解需求，构建可执行方案", "reviewer" => "独立审查，发现风险与边界", _ => "整合观点，交付最终答案" };

    public AgentModelProfile? Profile
    {
        get => profile;
        set => SetProperty(ref profile, value);
    }

    public string Instructions
    {
        get => instructions;
        set => SetProperty(ref instructions, value);
    }

    public AgentWorkflowRoleViewModel(string id, string name, string instructions)
    {
        Id = id;
        Name = name;
        this.instructions = instructions;
    }
}

public sealed class AgentWorkflowStepViewModel : ObservableObject
{
    private string output = "";
    private string error = "";
    private AgentStepStatus status;
    public string Id { get; }
    public string Name { get; }
    public string Model { get; }
    public string Number => Id switch { "analyst" => "01", "reviewer" => "02", _ => "03" };
    public AgentStepStatus State => status;

    public string Output
    {
        get => output;
        private set
        {
            if (!SetProperty(ref output, value)) return;
            OnPropertyChanged(nameof(HasOutput));
            OnPropertyChanged(nameof(IsWaiting));
        }
    }

    public string Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    public bool IsGenerating => status == AgentStepStatus.Running;
    public bool HasOutput => !string.IsNullOrEmpty(Output);
    public bool IsWaiting => IsGenerating && !HasOutput;

    public string StatusText => status switch
    {
        AgentStepStatus.Running => "进行中",
        AgentStepStatus.Completed => "已完成",
        AgentStepStatus.Failed => "失败",
        AgentStepStatus.Cancelled => "已取消",
        AgentStepStatus.Skipped => "已跳过",
        _ => "等待中"
    };

    public AgentWorkflowStepViewModel(AgentRoleDefinition role)
    {
        Id = role.Id;
        Name = role.Name;
        Model = role.Model.Name + " / " + role.Model.Model;
    }

    public void Apply(AgentStepResult result)
    {
        if (SetProperty(ref status, result.Status))
        {
            OnPropertyChanged(nameof(IsGenerating));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsWaiting));
        }

        Output = result.Output;
        Error = result.Error;
    }
}

public sealed class AgentWorkflowViewModel : ObservableObject, IDisposable
{
    private readonly AgentOrchestrationService service;
    private CancellationTokenSource? cancellation;
    private Guid activeRunId;
    private bool isBusy;
    private bool consent;
    private bool disposed;
    private string task = "";
    private string status = "先配置模型，为三个角色选择模型，再开始任务。";
    private string finalOutput = "";
    private int templateIndex;
    private AgentWorkflowStepViewModel? selectedStep;
    private AgentWorkflowDefinition? activeDefinition;
    private AgentWorkflowRun? lastRun;
    private string? activeWorkspaceRoot;
    private bool resumeRequested;
    private bool generateFiles;
    private bool enableCommands;
    private AgentCommandSession? commandSession;
    private AgentCommandApproval? pendingCommand;
    private string workspacePath = "";
    private AgentFilePlan? filePlan;
    private readonly AgentWorkspaceWriter writer = new();
    public ObservableCollection<string> Operations { get; } = [];
    public bool GenerateFiles
    {
        get => generateFiles;
        set
        {
            if (!CanEditConfiguration || !SetProperty(ref generateFiles, value)) return;
            if (value) EnableCommands = false;
            ClearFilePlan();
            OnPropertyChanged(nameof(NeedsWorkspace));
        }
    }
    public bool EnableCommands
    {
        get => enableCommands;
        set
        {
            if (!CanEditConfiguration || !SetProperty(ref enableCommands, value)) return;
            if (value) GenerateFiles = false;
            ClearFilePlan();
            OnPropertyChanged(nameof(NeedsWorkspace));
        }
    }
    public bool NeedsWorkspace => GenerateFiles || EnableCommands;
    public AgentCommandApproval? PendingCommand
    {
        get => pendingCommand;
        private set
        {
            if (!SetProperty(ref pendingCommand, value)) return;
            OnPropertyChanged(nameof(HasPendingCommand));
            ApproveCommand.NotifyCanExecuteChanged();
            RejectCommand.NotifyCanExecuteChanged();
        }
    }
    public bool HasPendingCommand => PendingCommand is not null;
    public IRelayCommand ApproveCommand { get; }
    public IRelayCommand RejectCommand { get; }
    public string WorkspacePath { get => workspacePath; private set => SetProperty(ref workspacePath, value); }
    public bool CanPreviewFiles => IsIdle && filePlan is not null;
    public AgentFilePlan? FilePlan => filePlan;
    public bool HasResumeCheckpoint => activeDefinition is not null && lastRun is not null &&
        lastRun.Status != AgentStepStatus.Completed && lastRun.Steps.Any(step => step.Status != AgentStepStatus.Completed);
    public bool CanResume => IsIdle && Consent && HasResumeCheckpoint;
    public bool CanEditConfiguration => IsIdle && !HasResumeCheckpoint;

    public void SelectWorkspace(string path)
    {
        if (!CanEditConfiguration) return;
        WorkspacePath = AgentWorkspaceWriter.ValidateRoot(path);
        ClearFilePlan();
        Status = "工作目录已选择；文件清单须预览确认，命令模式须逐次批准。";
    }

    private void ClearFilePlan()
    {
        filePlan = null;
        OnPropertyChanged(nameof(CanPreviewFiles));
    }

    private void RecordOperation(string message)
    {
        Operations.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (Operations.Count > 150) Operations.RemoveAt(0);
    }

    public async Task ApplyFilesAsync(AgentFilePlan approvedPlan)
    {
        if (!CanPreviewFiles || !ReferenceEquals(approvedPlan, filePlan)) return;
        ClearFilePlan();
        IsBusy = true;
        using CancellationTokenSource current = new();
        cancellation = current;
        RecordOperation("用户已确认文件列表，开始写入：" + approvedPlan.Root);
        IProgress<string> progress = new Progress<string>(RecordOperation);
        try
        {
            AgentFileWriteResult result = await Task.Run(() => writer.Apply(approvedPlan, progress, current.Token));
            Status = result.Message;
        }
        catch (Exception)
        {
            Status = "文件写入未完成，请检查操作记录和目录权限；不会自动重试。";
        }
        finally
        {
            cancellation = null;
            IsBusy = false;
        }
    }

    public ObservableCollection<AgentModelProfile> Profiles { get; }

    public ObservableCollection<AgentWorkflowRoleViewModel> Roles { get; } =
    [
        new("analyst", "方案分析员", "分析任务，拆解需求与验收条件，给出可执行的方案；涉及代码时给出关键实现。明确假设和不确定性。"),
        new("reviewer", "风险审查员", "检查风险、边界条件和测试要点。有上游方案时审查该方案；没有时独立分析任务。指出具体问题，不要只表示同意。"),
        new("coordinator", "汇总定稿员", "结合原始任务和两份参考结果，处理分歧，修正方案，输出最终答案、验证建议和仍待确认的问题。")
    ];

    public ObservableCollection<AgentWorkflowStepViewModel> Steps { get; } = [];
    public IReadOnlyList<string> Templates { get; } = ["并行分析 → 汇总", "方案 → 审查 → 定稿"];
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand RetryCommand { get; }
    public IRelayCommand NewTaskCommand { get; }
    public IRelayCommand StopCommand { get; }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RefreshCommands();
            }
        }
    }

    public bool IsIdle => !IsBusy && !disposed;

    public bool Consent
    {
        get => consent;
        set
        {
            if (SetProperty(ref consent, value)) RefreshCommands();
        }
    }

    public string TaskText
    {
        get => task;
        set
        {
            if (SetProperty(ref task, value)) RefreshCommands();
        }
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public string FinalOutput
    {
        get => finalOutput;
        private set => SetProperty(ref finalOutput, value);
    }

    public int TemplateIndex
    {
        get => templateIndex;
        set => SetProperty(ref templateIndex, value);
    }

    public AgentWorkflowStepViewModel? SelectedStep
    {
        get => selectedStep;
        set => SetProperty(ref selectedStep, value);
    }

    public AgentWorkflowViewModel(AgentDebugViewModel workspace, AgentOrchestrationService service)
        : this(workspace.Profiles, service)
    {
    }

    internal AgentWorkflowViewModel(ObservableCollection<AgentModelProfile> profiles, AgentOrchestrationService service)
    {
        this.service = service;
        Profiles = profiles;
        ApproveCommand = new RelayCommand(() => DecideCommand(true), () => HasPendingCommand);
        RejectCommand = new RelayCommand(() => DecideCommand(false), () => HasPendingCommand);
        StartCommand =
            new AsyncRelayCommand(StartAsync, () => CanEditConfiguration && Consent && !string.IsNullOrWhiteSpace(TaskText));
        RetryCommand = new AsyncRelayCommand(ResumeAsync, () => CanResume);
        NewTaskCommand = new RelayCommand(() =>
        {
            activeDefinition = null;
            lastRun = null;
            activeWorkspaceRoot = null;
            commandSession = null;
            ClearFilePlan();
            Status = "已退出续跑模式，可以修改配置并开始新任务；旧输出仍保留供参考。";
            RefreshCommands();
        }, () => IsIdle && HasResumeCheckpoint);
        StopCommand = new RelayCommand(Cancel, () => IsBusy);
        RefreshProfiles();
    }

    public void RefreshProfiles()
    {
        if (!CanEditConfiguration) return;
        for (int index = 0; index < Roles.Count; index++)
            if (Roles[index].Profile is null || !Profiles.Contains(Roles[index].Profile!))
                Roles[index].Profile = Profiles.ElementAtOrDefault(index) ?? Profiles.FirstOrDefault();
    }

    private async Task StartAsync()
    {
        if (!IsIdle || !Consent) return;
        if (HasResumeCheckpoint && !resumeRequested) return;
        AgentWorkflowDefinition definition;
        AgentWorkflowRun? previousRun = null;
        bool isResume = resumeRequested;
        resumeRequested = false;
        string? approvedRoot = null;
        try
        {
            if (isResume)
            {
                definition = activeDefinition ?? throw new InvalidOperationException("没有可继续的任务。");
                previousRun = lastRun ?? throw new InvalidOperationException("没有可恢复的步骤记录。");
                approvedRoot = definition.GenerateFiles || definition.EnableCommands ? AgentWorkspaceWriter.ValidateRoot(activeWorkspaceRoot ?? "") : null;
            }
            else
            {
                if (NeedsWorkspace) approvedRoot = AgentWorkspaceWriter.ValidateRoot(WorkspacePath);
                if (TaskText.Length > AgentOrchestrationService.MaximumTaskLength)
                    throw new ArgumentException("任务不能超过 16000 个字符。");
                if (TemplateIndex is < 0 or > 1) throw new ArgumentException("请选择流程模板。");
                AgentRoleDefinition[] roles = Roles.Select(role =>
                {
                    if (role.Profile is null || !Profiles.Contains(role.Profile))
                        throw new ArgumentException($"请为{role.Name}选择有效模型。");
                    if (string.IsNullOrWhiteSpace(role.Instructions)) throw new ArgumentException($"请填写{role.Name}的角色提示词。");
                    return new AgentRoleDefinition(role.Id, role.Name, role.Instructions, role.Profile.CreateOptions());
                }).ToArray();
                definition = new AgentWorkflowDefinition(Guid.NewGuid(), TaskText.Trim(), (AgentWorkflowMode)TemplateIndex,
                    roles[0], roles[1], roles[2]) { GenerateFiles = GenerateFiles, EnableCommands = EnableCommands };
            }
        }
        catch (Exception exception) when (exception is ArgumentException or System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException)
        {
            Status = exception.Message;
            return;
        }

        activeDefinition = definition;
        activeRunId = definition.RunId;
        ClearFilePlan();
        if (!isResume)
        {
            activeWorkspaceRoot = approvedRoot;
            IProgress<string> commandProgress = new Progress<string>(message =>
            {
                if (!disposed && activeRunId == definition.RunId) RecordOperation(message);
            });
            commandSession = definition.EnableCommands
                ? new AgentCommandSession(approvedRoot!, RequestCommandApprovalAsync, commandProgress) : null;
            lastRun = null;
            Operations.Clear();
            RecordOperation(approvedRoot is null ? "开始文本协同，不具备文件写入权限。" :
                definition.EnableCommands ? "命令模式：" + approvedRoot + "；定稿员可请求命令，每次须确认。" :
                "本次文件输出目录：" + approvedRoot + "；等待模型生成，尚未写入。");
            Steps.Clear();
            foreach (AgentRoleDefinition role in new[] { definition.Analyst, definition.Reviewer, definition.Coordinator })
                Steps.Add(new AgentWorkflowStepViewModel(role));
            SelectedStep = Steps[0];
            FinalOutput = "";
            Status = "任务执行中；点击步骤可查看独立输出。不限制任务总时长，可手动停止。";
        }
        else
        {
            RecordOperation("继续任务：保留已完成步骤，仅重试未完成步骤。");
            Status = "正在继续；已完成步骤不会重复调用模型。";
        }
        OnPropertyChanged(nameof(CanResume));
        using CancellationTokenSource current = new();
        cancellation = current;
        IsBusy = true;
        IProgress<AgentWorkflowUpdate> progress = new Progress<AgentWorkflowUpdate>(update =>
        {
            if (!disposed && IsBusy && ReferenceEquals(cancellation, current) && update.RunId == activeRunId)
            {
                AgentWorkflowStepViewModel? step = Steps.FirstOrDefault(step => step.Id == update.Step.StepId);
                if (step is not null && step.State != update.Step.Status)
                {
                    string operation = update.Step.Status switch
                    {
                        AgentStepStatus.Running => "已发起模型请求，等待流式输出",
                        AgentStepStatus.Completed => "模型输出完成",
                        AgentStepStatus.Failed => "调用失败",
                        AgentStepStatus.Cancelled => "已取消",
                        AgentStepStatus.Skipped => "已跳过",
                        _ => "等待中"
                    };
                    RecordOperation(update.Step.RoleName + "：" + operation);
                    if (!string.IsNullOrWhiteSpace(update.Step.Error)) RecordOperation(update.Step.Error);
                }
                step?.Apply(update.Step);
            }
        });
        try
        {
            AgentWorkflowRun result = await Task.Run(() => service.RunAsync(definition, progress, current.Token, previousRun, commandSession));
            foreach (AgentStepResult step in result.Steps) Steps.First(item => item.Id == step.StepId).Apply(step);
            FinalOutput = result.FinalOutput;
            lastRun = result;
            OnPropertyChanged(nameof(CanResume));
            Status = result.Message;
            if (result.Status == AgentStepStatus.Completed) SelectedStep = Steps[^1];
            else SelectedStep = Steps.FirstOrDefault(step => step.State == AgentStepStatus.Failed) ??
                Steps.FirstOrDefault(step => step.State != AgentStepStatus.Completed);
            if (result.Status != AgentStepStatus.Completed && SelectedStep is { Error.Length: > 0 } failedStep)
                RecordOperation(failedStep.Name + "：" + failedStep.Error);
            if (result.Status == AgentStepStatus.Completed && definition.GenerateFiles && approvedRoot is not null && !current.IsCancellationRequested && !disposed)
            {
                try
                {
                    AgentFilePlan prepared = await Task.Run(() => writer.Prepare(approvedRoot, result.FinalOutput));
                    if (!current.IsCancellationRequested && !disposed)
                    {
                        filePlan = prepared;
                        Status = $"已生成 {prepared.Files.Count} 个文件的待审变更，请点击“预览并写入”；尚未修改磁盘。";
                        RecordOperation(Status);
                    }
                }
                catch (Exception exception)
                {
                    string reason = exception switch
                    {
                        System.IO.InvalidDataException => exception.Message,
                        System.Text.Json.JsonException => "定稿员没有返回有效的 JSON 文件清单，或输出被截断。",
                        UnauthorizedAccessException => "没有权限读取授权目录中的文件。",
                        System.IO.IOException => "授权目录或目标文件不可访问，可能被占用。",
                        _ => "文件清单结构、路径或编码不符合要求。"
                    };
                    AgentStepResult failed = result.Steps[^1] with { Status = AgentStepStatus.Failed, Error = "文件清单校验失败：" + reason };
                    AgentStepResult[] retained = result.Steps.ToArray();
                    retained[^1] = failed;
                    Status = failed.Error + " 未写入文件；点击“重试并继续”仅重新生成定稿。";
                    lastRun = new AgentWorkflowRun(result.RunId, AgentStepStatus.Failed, Array.AsReadOnly(retained), Status);
                    Steps[^1].Apply(failed);
                    FinalOutput = "";
                    RecordOperation(Status);
                }
            }
        }
        catch (Exception)
        {
            Status = "协同任务未能完成；已保留当前步骤结果，请检查步骤错误后点击“重试并继续”。";
            AgentStepResult[] retained = Steps.Select(step => new AgentStepResult(step.Id, step.Name,
                step.State == AgentStepStatus.Running ? AgentStepStatus.Failed : step.State, step.Output,
                step.State == AgentStepStatus.Running ? "步骤意外中断，可重试。" : step.Error)).ToArray();
            lastRun = new AgentWorkflowRun(definition.RunId, AgentStepStatus.Failed, Array.AsReadOnly(retained), Status);
            foreach (AgentStepResult step in retained) Steps.First(item => item.Id == step.StepId).Apply(step);
            RecordOperation(Status);
        }
        finally
        {
            cancellation = null;
            IsBusy = false;
        }
    }

    private void DecideCommand(bool approved)
    {
        AgentCommandApproval? request = PendingCommand;
        if (request is null) return;
        PendingCommand = null;
        request.Decision.TrySetResult(approved);
    }

    private async Task<bool> RequestCommandApprovalAsync(AgentCommandApproval request, CancellationToken cancellationToken)
    {
        System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
        try
        {
            await dispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed) throw new OperationCanceledException(cancellationToken);
                PendingCommand = request;
            });
            return await request.Decision.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(PendingCommand, request)) PendingCommand = null;
            });
        }
    }

    public void Cancel()
    {
        if (cancellation is null || cancellation.IsCancellationRequested) return;
        Status = "正在停止所有步骤，请等待当前请求退出…";
        cancellation.Cancel();
        PendingCommand = null;
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanPreviewFiles));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(HasResumeCheckpoint));
        OnPropertyChanged(nameof(CanEditConfiguration));
        StartCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        NewTaskCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private async Task ResumeAsync()
    {
        if (!CanResume) return;
        resumeRequested = true;
        await StartAsync();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Cancel();
        activeDefinition = null;
        lastRun = null;
        activeWorkspaceRoot = null;
        commandSession = null;
        OnPropertyChanged(nameof(IsIdle));
        RefreshCommands();
    }
}
