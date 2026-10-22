using System.Collections.ObjectModel;
using System.ComponentModel;
using AIAgentFrameworkModule.Agent;
using AIAgentFrameworkModule.Models;
using AIAgentFrameworkModule.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAgentFrameworkModule.ViewModels;

public sealed class AgentGroupChatViewModel : ObservableObject, IDisposable
{
    private readonly IAgentStepExecutor executor;
    private readonly AgentGroupChatStore store;
    private CancellationTokenSource? cancellation;
    private string roomName = "AI 群友聊天室";
    private string topic = "轻松交流，认真回应；有不同意见也可以友善讨论。";
    private string draft = "";
    private string status = "先配置群友，再发一句话开启群聊。";
    private bool busy;
    private bool consent;
    private bool continuousChat;
    private bool disposed;
    private int rounds = 1;

    public ObservableCollection<AgentModelProfile> Profiles { get; }
    public ObservableCollection<AgentGroupMember> Members { get; } = [];
    public ObservableCollection<GroupChatMessage> Messages { get; } = [];
    public IReadOnlyList<int> RoundOptions { get; } = [1, 2, 3];

    public string RoomName
    {
        get => roomName;
        set
        {
            if (SetProperty(ref roomName, value)) Consent = false;
        }
    }

    public string Topic
    {
        get => topic;
        set
        {
            if (SetProperty(ref topic, value)) Consent = false;
        }
    }

    public string Draft
    {
        get => draft;
        set
        {
            if (SetProperty(ref draft, value)) RefreshCommands();
        }
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public bool Consent
    {
        get => consent;
        set
        {
            if (SetProperty(ref consent, value))
            {
                if (!value && IsBusy) Cancel();
                RefreshCommands();
            }
        }
    }

    public bool ContinuousChat
    {
        get => continuousChat;
        set
        {
            if (value && !IsIdle)
            {
                OnPropertyChanged();
                return;
            }
            if (!SetProperty(ref continuousChat, value)) return;
            OnPropertyChanged(nameof(ContinuousChatLabel));
            RefreshCommands();
            if (!value)
            {
                cancellation?.Cancel();
                return;
            }

            if (!Consent)
            {
                Status = "请先确认允许向模型服务商发送聊天记录。";
                ContinuousChat = false;
                return;
            }

            if (!Members.Any(member => member.Enabled))
            {
                Status = "请先启用至少一位群友。";
                ContinuousChat = false;
                return;
            }

            _ = RunContinuousAsync();
        }
    }

    public string ContinuousChatLabel => ContinuousChat ? "停止连续聊天" : "开始连续聊天";
    public bool CanToggleContinuousChat => !disposed && (ContinuousChat || IsIdle);

    private async Task RunContinuousAsync()
    {
        try
        {
            await RunAsync(!string.IsNullOrWhiteSpace(Draft));
        }
        catch (OperationCanceledException)
        {
            Status = "已停止群聊，已收到的消息会保留。";
        }
        catch (Exception)
        {
            Status = "连续聊天未能完成，请检查模型配置后重新开启。";
        }
        finally
        {
            ContinuousChat = false;
        }
    }

    public int Rounds
    {
        get => rounds;
        set
        {
            if (SetProperty(ref rounds, value))
            {
                Consent = false;
                OnPropertyChanged(nameof(RunSummary));
            }
        }
    }

    public bool IsBusy
    {
        get => busy;
        private set
        {
            if (SetProperty(ref busy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RefreshCommands();
            }
        }
    }

    public bool IsIdle => !IsBusy && !disposed;

    public string RunSummary =>
        ContinuousChat
            ? $"{Members.Count(member => member.Enabled)} 位 AI · 连续对话中 · 关闭开关即可停止"
            : $"{Members.Count(member => member.Enabled)} 位 AI · 普通发送 {Rounds} 轮 · 可开启连续聊天";

    public IAsyncRelayCommand SendCommand { get; }
    public IAsyncRelayCommand ContinueCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand AddMemberCommand { get; }
    public IRelayCommand<AgentGroupMember> RemoveMemberCommand { get; }

    public AgentGroupChatViewModel(AgentDebugViewModel workspace, IAgentStepExecutor executor,
        AgentGroupChatStore store) : this(workspace.Profiles, executor, store)
    {
    }

    internal AgentGroupChatViewModel(ObservableCollection<AgentModelProfile> profiles, IAgentStepExecutor executor,
        AgentGroupChatStore store)
    {
        Profiles = profiles;
        this.executor = executor;
        this.store = store;
        SendCommand = new AsyncRelayCommand(() => RunAsync(true), () => CanRun && !string.IsNullOrWhiteSpace(Draft));
        ContinueCommand = new AsyncRelayCommand(() => RunAsync(false),
            () => CanRun && Messages.Any(message => !string.IsNullOrWhiteSpace(message.Text)));
        StopCommand = new RelayCommand(Cancel);
        ClearCommand = new RelayCommand(() =>
        {
            Messages.Clear();
            Consent = false;
            Status = "聊天记录已清空。";
            RefreshCommands();
        }, () => IsIdle);
        SaveCommand = new RelayCommand(Save, () => IsIdle);
        AddMemberCommand = new RelayCommand(AddMember, () => IsIdle && Members.Count < 8);
        RemoveMemberCommand =
            new RelayCommand<AgentGroupMember>(RemoveMember,
                member => IsIdle && Members.Count > 1 && member is not null);
        Load();
    }

    private bool CanRun => IsIdle && Consent && Members.Any(member => member.Enabled);

    private void Load()
    {
        try
        {
            GroupChatSettings? settings = store.Load();
            if (settings is not null)
            {
                RoomName = settings.RoomName;
                Topic = settings.Topic;
                Rounds = settings.Rounds;
                foreach (GroupMemberSettings member in settings.Members)
                    Attach(new AgentGroupMember
                    {
                        Name = member.Name, Persona = member.Persona, Enabled = member.Enabled,
                        SavedReference = member
                    });
                RefreshProfiles();
                Status = "已加载群聊配置；聊天记录仅保留在本次运行内存中。";
                return;
            }
        }
        catch (Exception)
        {
            Status = "群聊配置读取失败，已使用默认群友；原文件未修改，保存前请检查。";
        }

        Attach(new AgentGroupMember
            { Name = "小晴", Persona = "温暖、善于倾听，像朋友一样接话，偶尔用轻松的表情。", Profile = Profiles.FirstOrDefault() });
        Attach(new AgentGroupMember
        {
            Name = "阿哲", Persona = "理性好奇，喜欢提出不同角度，但不说教、不做长篇总结。",
            Profile = Profiles.ElementAtOrDefault(1) ?? Profiles.FirstOrDefault()
        });
        Attach(new AgentGroupMember
        {
            Name = "可乐", Persona = "幽默活泼，善于把话题聊得有趣，不过度玩梗，不冒犯他人。",
            Profile = Profiles.ElementAtOrDefault(2) ?? Profiles.FirstOrDefault()
        });
    }

    private void Attach(AgentGroupMember member)
    {
        Members.Add(member);
        member.PropertyChanged += OnMemberChanged;
        OnPropertyChanged(nameof(RunSummary));
        RefreshCommands();
    }

    private void OnMemberChanged(object? sender, PropertyChangedEventArgs args)
    {
        Consent = false;
        OnPropertyChanged(nameof(RunSummary));
        RefreshCommands();
    }

    private void AddMember()
    {
        int number = 1;
        while (Members.Any(member => member.Name == $"群友 {number}")) number++;
        Attach(new AgentGroupMember { Name = $"群友 {number}", Profile = Profiles.FirstOrDefault() });
        Consent = false;
    }

    private void RemoveMember(AgentGroupMember? member)
    {
        if (member is null || !Members.Remove(member)) return;
        member.PropertyChanged -= OnMemberChanged;
        Consent = false;
        OnPropertyChanged(nameof(RunSummary));
        RefreshCommands();
    }

    public void RefreshProfiles()
    {
        if (!IsIdle) return;
        Consent = false;
        foreach (AgentGroupMember member in Members)
        {
            if (member.Profile is not null && Profiles.Contains(member.Profile)) continue;
            GroupMemberSettings? reference = member.SavedReference;
            AgentModelProfile[] matches = Profiles.Where(profile =>
                reference is not null && profile.Name == reference.ProfileName &&
                profile.Endpoint == reference.Endpoint && profile.Model == reference.Model).ToArray();
            member.Profile = matches.Length == 1 ? matches[0] : null;
        }

        RefreshCommands();
    }

    private GroupChatSettings Snapshot() => new()
    {
        RoomName = RoomName.Trim(), Topic = Topic, Rounds = Rounds,
        Members = Members.Select(member => new GroupMemberSettings
        {
            Name = member.Name.Trim(), Persona = member.Persona, Enabled = member.Enabled,
            ProfileName = member.Profile?.Name ?? member.SavedReference?.ProfileName ?? "",
            Endpoint = member.Profile?.Endpoint ?? member.SavedReference?.Endpoint ?? "",
            Model = member.Profile?.Model ?? member.SavedReference?.Model ?? ""
        }).ToList()
    };

    private void Save()
    {
        try
        {
            GroupChatSettings settings = Snapshot();
            store.Save(settings);
            for (int index = 0; index < Members.Count; index++) Members[index].SavedReference = settings.Members[index];
            Status = "群聊设置已保存；模型配置请在模型配置页单独保存。密钥与聊天记录未写入群聊配置。";
        }
        catch (System.IO.InvalidDataException exception)
        {
            Status = exception.Message;
        }
        catch (Exception)
        {
            Status = "群聊配置保存失败，请检查本地配置目录权限。";
        }
    }

    private async Task RunAsync(bool includeDraft)
    {
        if (!CanRun) return;
        GroupChatSettings settings;
        List<AgentModelOptions> participants = [];
        try
        {
            settings = Snapshot();
            AgentGroupChatStore.Validate(settings);
            if (includeDraft && (string.IsNullOrWhiteSpace(Draft) || Draft.Length > 4000))
                throw new System.IO.InvalidDataException("消息不能为空，且不能超过 4000 字。");
            string[] names = Members.Where(member => member.Enabled).Select(member => member.Name.Trim()).ToArray();
            foreach (AgentGroupMember member in Members.Where(member => member.Enabled))
            {
                if (member.Profile is null || !Profiles.Contains(member.Profile))
                    throw new System.IO.InvalidDataException($"请在群聊配置中为 {member.Name} 选择模型。");
                AgentModelOptions options = member.Profile.CreateOptions();
                options.Name = member.Name.Trim();
                options.Instructions =
                    AgentGroupChatContext.Instructions(options.Name, member.Persona, settings.Topic, names);
                participants.Add(options);
            }
        }
        catch (System.IO.InvalidDataException exception)
        {
            Status = exception.Message;
            return;
        }
        catch (Exception)
        {
            Status = "某位群友的模型配置不可用，请检查模型、地址、密钥和超时设置。未发送消息。";
            return;
        }
        using CancellationTokenSource current = new();
        cancellation = current;
        IsBusy = true;
        long failures = 0;
        if (includeDraft)
        {
            AddMessage(new GroupChatMessage { Speaker = "我", IsUser = true, Text = Draft.Trim() });
            Draft = "";
        }

        try
        {
            long round = 0;
            int failedRounds = 0;
            while (ContinuousChat || round < settings.Rounds)
            {
                current.Token.ThrowIfCancellationRequested();
                round++;
                long failuresBeforeRound = failures;
                foreach (AgentModelOptions options in participants)
                {
                    current.Token.ThrowIfCancellationRequested();
                    string prompt = AgentGroupChatContext.Build(Messages, options.Name);
                    GroupChatMessage reply = new()
                    {
                        Speaker = options.Name, Model = options.Model,
                        IsGenerating = true, Status = "正在输入…"
                    };
                    AddMessage(reply);
                    Status = ContinuousChat
                        ? $"{options.Name} 正在接话 · 连续聊天第 {round} 轮 · 可随时关闭开关停止"
                        : $"{options.Name} 正在接话 · 第 {round}/{settings.Rounds} 轮";
                    using CancellationTokenSource timeout =
                        CancellationTokenSource.CreateLinkedTokenSource(current.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(options.NetworkTimeoutSeconds));
                    try
                    {
                        await foreach (string part in executor.ExecuteAsync(options, prompt, timeout.Token))
                        {
                            current.Token.ThrowIfCancellationRequested();
                            int remaining = AgentGroupChatContext.MaximumReplyCharacters - reply.Text.Length;
                            reply.Text += part.Length > remaining ? part[..remaining] : part;
                            if (reply.Text.Length >= AgentGroupChatContext.MaximumReplyCharacters)
                            {
                                reply.Status = "回复已达长度上限";
                                timeout.Cancel();
                                break;
                            }
                        }

                        current.Token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(reply.Text))
                        {
                            reply.Status = "未返回文字，已跳过";
                            failures++;
                        }
                        else if (reply.Status == "正在输入…") reply.Status = "";
                    }
                    catch (OperationCanceledException) when (current.IsCancellationRequested)
                    {
                        reply.Status = "已停止，保留已收到的内容";
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        reply.Status = "回复超时，已跳过；可继续聊天";
                        failures++;
                    }
                    catch (Exception)
                    {
                        reply.Status = "回复失败，请检查该成员模型配置；其他群友继续";
                        failures++;
                    }
                    finally
                    {
                        reply.IsGenerating = false;
                    }
                    if (ContinuousChat) await Task.Delay(TimeSpan.FromSeconds(1), current.Token);
                }
                failedRounds = failures - failuresBeforeRound == participants.Count ? failedRounds + 1 : 0;
                if (ContinuousChat && failedRounds >= 2)
                {
                    Status = "连续两轮所有成员均回复失败或为空，已自动停止，避免持续无效调用。请检查模型服务后重新开启。";
                    return;
                }
            }

            current.Token.ThrowIfCancellationRequested();
            Status = failures == 0
                ? "大家聊完了，可以发消息或开启连续聊天。"
                : $"本次聊天结束，{failures} 次回复未完成；已保留收到的内容。";
        }
        catch (OperationCanceledException)
        {
            Status = "已停止群聊，已收到的消息会保留。";
        }
        catch (Exception)
        {
            Status = "群聊意外中断，已收到的消息会保留。";
        }
        finally
        {
            cancellation = null;
            if (ContinuousChat) ContinuousChat = false;
            IsBusy = false;
        }
    }

    private void AddMessage(GroupChatMessage message)
    {
        GroupChatMessage? previous = Messages.LastOrDefault();
        message.ShowTimestamp = previous is null || message.CreatedAt.Date != previous.CreatedAt.Date ||
            message.CreatedAt - previous.CreatedAt >= TimeSpan.FromMinutes(5);
        Messages.Add(message);
        while (Messages.Count > 200) Messages.RemoveAt(0);
        Messages[0].ShowTimestamp = true;
    }

    public void Cancel()
    {
        if (ContinuousChat) ContinuousChat = false;
        cancellation?.Cancel();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Cancel();
        foreach (AgentGroupMember member in Members) member.PropertyChanged -= OnMemberChanged;
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        SendCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanToggleContinuousChat));
        OnPropertyChanged(nameof(RunSummary));
        SaveCommand.NotifyCanExecuteChanged();
        AddMemberCommand.NotifyCanExecuteChanged();
        RemoveMemberCommand.NotifyCanExecuteChanged();
    }
}
