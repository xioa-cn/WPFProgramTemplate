using AIAgentFrameworkModule.Agent;

namespace AIAgentFrameworkModule.Examples;

/// <summary>
/// 可以主动调用的入门示例，不会在模块启动时自动运行。
/// 示例 1、2、3 使用 AI_ENDPOINT、AI_MODEL、AI_API_KEY 环境变量。
/// 每次请求可能产生服务商费用，不要在构造函数或循环里无意重复调用。
/// </summary>
public static class AgentExamples
{
    /// <summary>示例 1：创建助手、提问、取得完整回答，方法结束时释放会话。</summary>
    /// <param name="cancellationToken">用于停止本次调用的取消令牌。</param>
    /// <returns>AI 生成的完整文字。</returns>
    public static async Task<string> AskOnceAsync(CancellationToken cancellationToken = default)
    {
        AgentModelOptions options = CreateOptionsFromEnvironment();
        AgentCore core = new AgentCore();

        // using 表示退出当前方法时自动调用 Dispose，不是导入命名空间的 using。
        using AgentConversation chat = core.CreateChat(options);

        // await 表示异步等待；不要使用 .Result 或 .Wait() 阻塞 WPF 界面。
        string answer = await chat.AskAsync("我是 C# 初学者，请解释什么是类。", cancellationToken);
        return answer;
    }

    /// <summary>示例 2：同一个会话连续提问，随后重置上下文。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>两轮回答，方便在界面或调试输出中查看。</returns>
    public static async Task<string> ChatWithHistoryAsync(CancellationToken cancellationToken = default)
    {
        AgentCore core = new AgentCore();
        using AgentConversation chat = core.CreateChat(CreateOptionsFromEnvironment());

        string firstAnswer = await chat.AskAsync("我叫小明，正在学习 C#。", cancellationToken);
        string secondAnswer = await chat.AskAsync("我叫什么？我正在学习什么？", cancellationToken);

        // ResetAsync 清空当前本地上下文，不影响模型配置，也不是删除服务商的记录。
        await chat.ResetAsync(cancellationToken);
        return "第一轮：" + firstAnswer + Environment.NewLine + "第二轮：" + secondAnswer;
    }

    /// <summary>示例 3：每收到一小段文字就通知调用者，方便逐步更新界面。</summary>
    /// <param name="onText">普通 C# 回调，例如 text => AnswerText += text。</param>
    /// <param name="cancellationToken">取消令牌，取消后会抛出 OperationCanceledException。</param>
    public static async Task AskStreamingAsync(
        Action<string> onText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onText);
        AgentCore core = new AgentCore();
        using AgentConversation chat = core.CreateChat(CreateOptionsFromEnvironment());

        // await foreach 逐段读取异步数据流；片段可能是一个字，也可能是一整句话。
        await foreach (string text in chat.AskStreamingAsync("请用简单示例解释 C# 属性。", cancellationToken))
        {
            // 回调发生在当前异步调用的上下文；后台线程调用时请自行切回 UI 线程。
            onText(text);
        }
    }

    /// <summary>
    /// 示例 4：两个模型分别回答同一个问题，两份配置可以使用不同厂商或中转站。
    /// 使用顺序调用方便学习，不涉及任务并发或复杂的多 Agent 编排。
    /// </summary>
    /// <param name="firstOptions">第一个模型的配置。</param>
    /// <param name="secondOptions">第二个模型的配置。</param>
    /// <param name="question">分别发给两个模型的问题。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>两个模型的回答。</returns>
    public static async Task<string> CompareModelsAsync(
        AgentModelOptions firstOptions,
        AgentModelOptions secondOptions,
        string question,
        CancellationToken cancellationToken = default)
    {
        AgentCore core = new AgentCore();
        using AgentConversation firstChat = core.CreateChat(firstOptions);
        using AgentConversation secondChat = core.CreateChat(secondOptions);

        // 每个会话绑定自己的模型、地址和密钥，两者的聊天历史互不混用。
        string firstAnswer = await firstChat.AskAsync(question, cancellationToken);
        string secondAnswer = await secondChat.AskAsync(question, cancellationToken);
        return "模型一：" + firstAnswer + Environment.NewLine + "模型二：" + secondAnswer;
    }

    /// <summary>从环境变量读取配置，不把真实密钥硬编码到源码中。</summary>
    /// <returns>尚未连接服务的配置对象。</returns>
    private static AgentModelOptions CreateOptionsFromEnvironment()
    {
        AgentModelOptions options = new AgentModelOptions();

        // Environment.GetEnvironmentVariable 是 .NET 方法，不是 AI SDK 方法。
        // ?? 表示左边为 null 时使用右边的值；缺少配置时由 Validate 给出错误提示。
        options.Endpoint = Environment.GetEnvironmentVariable("AI_ENDPOINT") ?? "https://api.openai.com/v1";
        options.Model = Environment.GetEnvironmentVariable("AI_MODEL") ?? "";
        options.ApiKey = Environment.GetEnvironmentVariable("AI_API_KEY") ?? "";
        options.ApiType = AgentApiType.ChatCompletions;
        return options;
    }
}
