using System.ClientModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using AIAgentFrameworkModule.Agent;
using AIAgentFrameworkModule.Models;
using AIAgentFrameworkModule.Services;
using Machine.ModuleLoad.Logger;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAgentFrameworkModule.ViewModels;

/// <summary>
/// 两个调试页面共享的状态。只在 WPF UI 线程调用，不使用 Task.Run 更新绑定集合。
/// 这里只管理一个调试会话，不是供不同用户共享的聊天服务。
/// </summary>
public class AgentDebugViewModel : ObservableObject, IDisposable
{
    private readonly AgentCore core;
    private readonly AgentProfileStore store;
    private AgentModelProfile? selectedProfile;
    private AgentConversation? conversation;
    private CancellationTokenSource? requestCancellation;
    private string prompt = "";
    private string status = "先填写模型配置，再到对话页发送问题。";
    private string activeModel = "尚未创建会话";
    private string conversationTitle = "新对话";
    private bool isBusy;
    private bool useStreaming = true;
    private bool releaseAfterRequest;
    private bool disposed;

    /// <summary>ObservableCollection 在增删项目时通知 WPF 刷新列表。</summary>
    public ObservableCollection<AgentModelProfile> Profiles { get; } = new ObservableCollection<AgentModelProfile>();

    /// <summary>仅用于调试显示的消息，不自动保存到磁盘。</summary>
    public ObservableCollection<AgentChatMessage> Messages { get; } = new ObservableCollection<AgentChatMessage>();

    /// <summary>当前选中的模型配置；切换模型时绝不把旧会话历史发送到新的服务。</summary>
    public AgentModelProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (IsBusy || ReferenceEquals(selectedProfile, value)) return;
            if (selectedProfile != null) selectedProfile.PropertyChanged -= OnProfileChanged;
            SetProperty(ref selectedProfile, value);
            if (selectedProfile != null) selectedProfile.PropertyChanged += OnProfileChanged;
            ReleaseConversation();
            Messages.Clear();
            ConversationTitle = "新对话";
            Status = "已切换配置。下一次发送将使用新的模型与独立会话。";
            GlobalLogger.DebuggerLogger?.Debug(
                $"[AI.Config] 已切换模型配置：{selectedProfile?.Name ?? "（无）"}；旧会话与显示记录已清空。");
            OnPropertyChanged(nameof(CanEditProfile));
            RefreshCommands();
        }
    }

    /// <summary>输入框中的问题；每次输入后重新判断发送按钮是否可用。</summary>
    public string Prompt
    {
        get => prompt;
        set
        {
            if (SetProperty(ref prompt, value)) SendCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>操作结果，不显示服务商原始响应，以免泄漏请求内容或密钥。</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>真正创建的会话所绑定的模型编号，与尚未应用的配置区分。</summary>
    public string ActiveModel { get => activeModel; private set => SetProperty(ref activeModel, value); }

    /// <summary>顶部简短标题，从第一条问题截取，仅用于显示。</summary>
    public string ConversationTitle { get => conversationTitle; private set => SetProperty(ref conversationTitle, value); }

    /// <summary>是否正在请求；统一禁用模型切换、配置编辑和重复发送。</summary>
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(CanEditProfile));
            RefreshCommands();
        }
    }

    /// <summary>请求期间不可执行的界面操作绑定此属性。</summary>
    public bool IsIdle => !IsBusy && !disposed;

    /// <summary>有配置且没有请求时才能编辑。</summary>
    public bool CanEditProfile => IsIdle && SelectedProfile != null;

    /// <summary>启用时逐段显示回答；关闭时等待完整结果。两种模式复用同一个会话。</summary>
    public bool UseStreaming { get => useStreaming; set => SetProperty(ref useStreaming, value); }

    /// <summary>界面显示的配置文件位置，文件不含直接输入的 API Key。</summary>
    public string ConfigurationPath => store.FilePath;

    /// <summary>RelayCommand 是 MVVM Toolkit 的同步命令，将按钮与普通方法连接。</summary>
    public RelayCommand AddProfileCommand { get; }
    /// <summary>删除当前内存配置；点击保存配置后才更新磁盘。</summary>
    public RelayCommand DeleteProfileCommand { get; }
    /// <summary>保存全部非密钥配置。</summary>
    public RelayCommand SaveProfilesCommand { get; }
    /// <summary>仅校验配置格式，不发送网络请求。</summary>
    public RelayCommand ValidateCommand { get; }
    /// <summary>AsyncRelayCommand 是 MVVM Toolkit 的异步命令，等待连接验证完成。</summary>
    public AsyncRelayCommand TestConnectionCommand { get; }
    /// <summary>发送用户问题的异步命令，内部捕获错误并更新状态。</summary>
    public AsyncRelayCommand SendCommand { get; }
    /// <summary>向当前请求发出取消信号，不直接释放仍在使用的会话。</summary>
    public RelayCommand StopCommand { get; }
    /// <summary>丢弃上下文并清空当前调试消息。</summary>
    public RelayCommand NewConversationCommand { get; }

    /// <summary>创建命令、读取本地配置。构造时绝不访问模型服务。</summary>
    public AgentDebugViewModel(AgentCore core, AgentProfileStore store)
    {
        this.core = core;
        this.store = store;
        // 命令的第二个参数决定按钮是否可用；NotifyCanExecuteChanged 会通知按钮重新判断。
        AddProfileCommand = new RelayCommand(AddProfile, () => IsIdle && Profiles.Count < 100);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => CanEditProfile);
        SaveProfilesCommand = new RelayCommand(SaveProfiles, () => IsIdle);
        ValidateCommand = new RelayCommand(ValidateProfile, () => CanEditProfile);
        TestConnectionCommand = new AsyncRelayCommand(() => RunRequestAsync(true), () => CanEditProfile);
        SendCommand = new AsyncRelayCommand(() => RunRequestAsync(false),
            () => CanEditProfile && !string.IsNullOrWhiteSpace(Prompt));
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        NewConversationCommand = new RelayCommand(NewConversation, () => IsIdle);

        try
        {
            foreach (AgentModelProfile profile in store.Load()) Profiles.Add(profile);
            if (Profiles.Count == 0) Profiles.Add(new AgentModelProfile());
            SelectedProfile = Profiles[0];
            Status = "配置已加载；密钥请输入或通过环境变量提供。不会自动连接模型。";
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 调试工作区已创建：{Profiles.Count} 份配置，文件={store.FilePath}。");
        }
        catch (Exception exception)
        {
            // 损坏文件不自动覆盖，保留给用户检查或备份。
            GlobalLogger.Error("[AI.Config] 读取模型配置失败，已创建临时配置且不覆盖原文件。", exception);
            Profiles.Add(new AgentModelProfile());
            SelectedProfile = Profiles[0];
            Status = "读取配置失败。已创建临时配置，原文件未覆盖；保存前请备份原文件。";
        }
    }

    /// <summary>新增一份模型配置，不会自动连接或自动保存。</summary>
    private void AddProfile()
    {
        AgentModelProfile profile = new AgentModelProfile();
        profile.Name = "模型 " + (Profiles.Count + 1);
        Profiles.Add(profile);
        SelectedProfile = profile;
        Status = "已新增配置，请填写并保存。密钥只在本次运行中保留。";
        GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 已新增配置：{profile.Name}，当前共 {Profiles.Count} 份。");
    }

    /// <summary>删除选中配置并丢弃对应聊天；更新磁盘必须主动点击保存。</summary>
    private void DeleteProfile()
    {
        AgentModelProfile? profile = SelectedProfile;
        SelectedProfile = null;
        if (profile != null)
        {
            profile.ApiKey = "";
            Profiles.Remove(profile);
        }
        SelectedProfile = Profiles.FirstOrDefault();
        RefreshCommands();
        Status = "配置已从内存删除；点击保存配置后才会更新磁盘文件。";
        GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 已从内存删除模型配置，当前剩余 {Profiles.Count} 份；磁盘文件需点击保存后更新。");
    }

    /// <summary>允许保存尚未填完的配置，但明确排除密钥和聊天记录。</summary>
    private void SaveProfiles()
    {
        try
        {
            store.Save(Profiles);
            Status = "配置已保存。直接输入的密钥、聊天记录均未写入文件。";
            GlobalLogger.Info($"[AI.Config] 模型配置已保存：共 {Profiles.Count} 份。");
        }
        catch (Exception exception)
        {
            GlobalLogger.Error("[AI.Config] 保存模型配置失败。", exception);
            Status = "保存失败，请检查配置目录权限和磁盘空间。";
        }
    }

    /// <summary>检查本地配置，不进行联网连通性测试，不产生模型调用费用。</summary>
    private void ValidateProfile()
    {
        try
        {
            SelectedProfile!.CreateOptions();
            Status = "配置格式正确；尚未验证密钥权限、模型是否存在及服务连通性。";
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 配置格式检查通过：{SelectedProfile.Name}。");
        }
        catch (ArgumentException exception)
        {
            Status = exception.Message;
            GlobalLogger.Warn($"[AI.Config] 配置格式检查失败：{exception.Message}");
        }
    }

    /// <summary>配置被修改后丢弃旧会话；旧消息仅保留用于观察，不再发送给模型。</summary>
    private void OnProfileChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsBusy)
        {
            releaseAfterRequest = true;
        }
        else
        {
            ReleaseConversation();
        }
        Status = "配置已修改。下一次发送将开始独立会话；如需持久化请点击保存配置。";
        GlobalLogger.DebuggerLogger?.Debug("[AI.Config] 检测到配置修改，旧会话将在请求结束或空闲时释放。");
    }

    /// <summary>
    /// 统一执行连接验证或聊天。连接验证使用临时会话，不把测试问题混入聊天上下文。
    /// 全部 await 保留 UI 上下文，集合及流式文本更新都回到 WPF 线程。
    /// </summary>
    private async Task RunRequestAsync(bool isConnectionTest)
    {
        if (!CanEditProfile) return;
        AgentModelOptions options;
        try
        {
            options = SelectedProfile!.CreateOptions();
        }
        catch (ArgumentException exception)
        {
            Status = exception.Message;
            GlobalLogger.Warn($"[AI.Config] 模型配置无效，已取消发送：{exception.Message}");
            return;
        }

        requestCancellation = new CancellationTokenSource();
        releaseAfterRequest = false;
        IsBusy = true;
        GlobalLogger.DebuggerLogger?.Debug(
            isConnectionTest
                ? $"[AI.Chat] 开始连接测试：模型={options.Model}，地址={options.Endpoint}。"
                : $"[AI.Chat] 开始发送问题：模型={options.Model}，地址={options.Endpoint}，流式={UseStreaming}，字符数={Prompt.Trim().Length}。");
        Stopwatch timer = Stopwatch.StartNew();
        AgentChatMessage? reply = null;
        try
        {
            if (isConnectionTest)
            {
                Status = "正在发送真实测试请求，可能产生少量费用…";
                // 临时会话在请求结束后释放，不影响对话页正在保存的上下文。
                using AgentConversation probe = core.CreateChat(options);
                string answer = await probe.AskAsync("请只回复：连接成功。", requestCancellation.Token);
                Status = string.IsNullOrWhiteSpace(answer)
                    ? "请求完成，但模型没有返回文本。请检查模型能力。"
                    : "连接验证成功，收到文本响应。耗时 " + timer.Elapsed.TotalSeconds.ToString("F1") + " 秒。";
                if (string.IsNullOrWhiteSpace(answer))
                    GlobalLogger.Warn($"[AI.Chat] 连接测试未收到文本：模型={options.Model}。");
                else
                    GlobalLogger.Success(
                        $"[AI.Chat] 连接测试成功：模型={options.Model}，耗时 {timer.Elapsed.TotalSeconds.ToString("F1")} 秒。");
                return;
            }

            if (conversation == null)
            {
                conversation = core.CreateChat(options);
                ActiveModel = options.Name + " / " + options.Model;
                GlobalLogger.DebuggerLogger?.Debug($"[AI.Chat] 已创建独立会话：{ActiveModel}。");
                if (Messages.Count > 0)
                    Messages.Add(new AgentChatMessage("系统", "已开始独立会话；之前的显示记录不会发送给模型。"));
            }

            string question = Prompt.Trim();
            if (Messages.Count == 0)
            {
                string title = question.Replace('\r', ' ').Replace('\n', ' ');
                // StringInfo 按文本元素截断，避免把 Emoji 的代理项或组合序列切成半个字符。
                System.Globalization.StringInfo titleElements = new System.Globalization.StringInfo(title);
                ConversationTitle = titleElements.LengthInTextElements > 24
                    ? titleElements.SubstringByTextElements(0, 24) + "…" : title;
            }
            Prompt = "";
            Messages.Add(new AgentChatMessage("你", question));
            reply = new AgentChatMessage("助手", "");
            reply.IsGenerating = true;
            Messages.Add(reply);
            Status = "正在等待模型回答…";

            if (UseStreaming)
            {
                await ReceiveStreamingReplyAsync(conversation, question, reply, requestCancellation.Token);
            }
            else
            {
                reply.Text = await conversation.AskAsync(question, requestCancellation.Token);
            }

            if (string.IsNullOrEmpty(reply.Text)) reply.Text = "（模型没有返回文本内容）";
            Status = "回答完成 · " + timer.Elapsed.TotalSeconds.ToString("F1") + " 秒 · " + reply.Text.Length + " 字符（非 Token 用量）";
            GlobalLogger.DebuggerLogger?.Debug(
                $"[AI.Chat] 本轮回答完成：{reply.Text.Length} 字符，耗时 {timer.Elapsed.TotalSeconds.ToString("F1")} 秒，流式={UseStreaming}。");
        }
        catch (OperationCanceledException)
        {
            releaseAfterRequest = true;
            Status = "请求已停止，下一次发送将使用新会话；服务商可能已经处理或计费。";
            GlobalLogger.Warn($"[AI.Chat] 本轮请求已取消，耗时 {timer.Elapsed.TotalSeconds.ToString("F1")} 秒。");
            if (!isConnectionTest) Messages.Add(new AgentChatMessage("系统", "本轮已取消，保留已收到的文本，旧上下文已丢弃。"));
        }
        catch (Exception exception)
        {
            releaseAfterRequest = true;
            Status = DescribeFailure(exception);
            GlobalLogger.Error($"[AI.Chat] {Status}", exception);
            if (!isConnectionTest) Messages.Add(new AgentChatMessage("系统", Status + " 下一次发送将使用新会话。"));
        }
        finally
        {
            if (reply != null && string.IsNullOrEmpty(reply.Text)) reply.Text = "（未收到文本）";
            if (reply != null) reply.IsGenerating = false;
            requestCancellation.Dispose();
            requestCancellation = null;
            if (releaseAfterRequest) ReleaseConversation();
            IsBusy = false;
        }
    }

    private static async Task ReceiveStreamingReplyAsync(
        AgentConversation chat, string question, AgentChatMessage reply, CancellationToken cancellationToken)
    {
        // StringBuilder.Append 追加字符，直到发布给界面时才创建完整字符串。
        StringBuilder buffer = new StringBuilder();
        DispatcherTimer publishTimer = new DispatcherTimer(DispatcherPriority.Background);
        publishTimer.Interval = TimeSpan.FromMilliseconds(33);
        publishTimer.Tick += (sender, args) =>
        {
            if (buffer.Length != reply.Text.Length) reply.Text = buffer.ToString();
        };
        Stopwatch yieldTimer = Stopwatch.StartNew();
        publishTimer.Start();
        try
        {
            await foreach (string part in chat.AskStreamingAsync(question, cancellationToken))
            {
                buffer.Append(part);
                if (reply.Text.Length == 0) reply.Text = buffer.ToString();
                if (yieldTimer.ElapsedMilliseconds >= 16)
                {
                    // 已缓存的网络片段可能同步返回，主动让出 UI 线程给输入、停止按钮和绘制。
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    cancellationToken.ThrowIfCancellationRequested();
                    yieldTimer.Restart();
                }
            }
        }
        finally
        {
            publishTimer.Stop();
            // 完成、取消和失败都发布最后的片段，不能因节流丢掉已经收到的内容。
            reply.Text = buffer.ToString();
        }
    }

    /// <summary>只显示可操作的错误分类，不直接展示服务端原始错误体。</summary>
    private static string DescribeFailure(Exception exception)
    {
        // JsonException 表示服务返回的不是合法 JSON，几乎都是 HTML 页面；地址或前缀不对时会命中网站首页。
        if (exception is JsonException)
        {
            return "服务返回的不是 JSON（很可能是 HTML 错误页或网站首页）。请检查基础地址是否为接口地址、是否缺少 /v1 前缀，以及中转站是否返回了错误页。";
        }

        // ClientResultException 是 System.ClientModel 的 HTTP 请求异常，Status 是 HTTP 状态码。
        if (exception is ClientResultException requestError)
        {
            if (requestError.Status == 401 || requestError.Status == 403) return "鉴权失败，请检查密钥、服务商及模型访问权限。";
            if (requestError.Status == 404) return "接口或模型不存在，请检查基础地址、模型编号和 Chat Completions 支持情况。";
            if (requestError.Status == 429) return "服务限流或额度不足，请检查服务商额度后再重试。";
            return "服务请求失败，HTTP 状态码：" + requestError.Status + "。请检查协议与服务状态。";
        }
        return "请求失败（" + exception.GetType().Name + "），请检查网络、超时和模型配置。原始错误未展示，避免泄漏敏感信息。";
    }

    /// <summary>取消当前请求，清理由异步方法的 finally 完成，避免释放使用中的客户端。</summary>
    private void Stop()
    {
        requestCancellation?.Cancel();
        Status = "正在停止，请等待当前请求退出…";
    }

    /// <summary>手动开始新对话，不修改模型配置。</summary>
    private void NewConversation()
    {
        ReleaseConversation();
        Messages.Clear();
        ConversationTitle = "新对话";
        Status = "已清空上下文。下一次发送将开始新会话。";
    }

    /// <summary>页面离开时停止请求并释放客户端，保留配置和可见记录供再次进入查看。</summary>
    public void Deactivate()
    {
        if (IsBusy)
        {
            releaseAfterRequest = true;
            Stop();
        }
        else
        {
            ReleaseConversation();
        }
    }

    /// <summary>只在请求已退出或没有请求时调用，避免 AgentConversation 的并发释放保护。</summary>
    private void ReleaseConversation()
    {
        conversation?.Dispose();
        conversation = null;
        ActiveModel = "下一次发送将创建新会话";
    }

    /// <summary>通知所有命令重新计算 CanExecute，使按钮状态与当前操作保持一致。</summary>
    private void RefreshCommands()
    {
        AddProfileCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        SaveProfilesCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        NewConversationCommand.NotifyCanExecuteChanged();
    }

    /// <summary>容器退出时停止请求并清除内存密钥；异步请求如未退出，会在 finally 中释放会话。</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        GlobalLogger.DebuggerLogger?.Debug("[AI.Config] 调试工作区已释放，内存中的密钥已清空。");
        if (SelectedProfile != null) SelectedProfile.PropertyChanged -= OnProfileChanged;
        Deactivate();
        foreach (AgentModelProfile profile in Profiles) profile.ApiKey = "";
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanEditProfile));
        RefreshCommands();
    }
}
