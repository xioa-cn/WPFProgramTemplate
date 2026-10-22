namespace AIAgentFrameworkModule.Agent;

using System.ClientModel;
using Machine.ModuleLoad.Logger;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

/// <summary>助手工厂，不保存聊天历史。每次 CreateChat 创建一个独立会话。</summary>
public class AgentCore
{
    /// <summary>
    /// 根据配置创建助手，支持 OpenAI 及相同协议的兼容服务、中转站。
    /// 创建时不调用模型；首次提问才发送请求。
    /// </summary>
    /// <param name="options">模型、地址、密钥、协议和提示词。</param>
    /// <returns>独立会话，使用完毕后 Dispose，或放在 using 中。</returns>
    public AgentConversation CreateChat(AgentModelOptions options, IList<AITool>? tools = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        Uri endpoint = options.Validate();

        // Endpoint 已补上结尾斜杠，SDK 在它后面拼接 chat/completions；先记下真实请求地址，便于排查 404 或返回 HTML 的情况。
        Uri requestAddress = new Uri(endpoint, "chat/completions");
        GlobalLogger.DebuggerLogger?.Debug(
            $"[AI.Agent] 创建会话：助手={options.Name}，模型={options.Model}，请求地址={requestAddress}，协议={options.ApiType}，超时={options.NetworkTimeoutSeconds} 秒。");
        if (endpoint.AbsolutePath == "/")
        {
            GlobalLogger.DebuggerLogger?.Warn(
                $"[AI.Agent] 基础地址 {endpoint} 没有接口前缀（例如 /v1）；多数兼容服务需要前缀，否则请求会命中网站页面而不是接口。");
        }

        // ApiKeyCredential 是 System.ClientModel 的凭据对象，SDK 根据它生成鉴权请求头。
        ApiKeyCredential credential = new ApiKeyCredential(options.ApiKey);

        // OpenAIClientOptions 是 OpenAI SDK 的连接配置。
        // Endpoint 决定请求发到哪里；修改它就是中转适配的关键。
        // NetworkTimeout 限制网络操作等待时间，总调用截止时间使用 CancellationToken 控制。
        OpenAIClientOptions clientOptions = new OpenAIClientOptions();
        clientOptions.Endpoint = endpoint;
        clientOptions.NetworkTimeout = TimeSpan.FromSeconds(options.NetworkTimeoutSeconds);

        // OpenAIClient 是 OpenAI SDK 入口，这里只使用未标记为实验性的聊天补全 API。
        OpenAIClient openAIClient = new OpenAIClient(credential, clientOptions);

        // IChatClient 是 Microsoft.Extensions.AI 的统一聊天接口，让上层不依赖具体模型品牌。
        // GetChatClient(model) 创建指定模型的聊天补全客户端。
        // AsIChatClient() 是微软的扩展方法，将 ChatClient 转换为统一接口。
        // 只使用 ChatClient 重载，不使用当前包中标记为实验性的 Responses 重载。
        IChatClient chatClient = openAIClient.GetChatClient(options.Model).AsIChatClient();

        try
        {
            return CreateChat(chatClient, options.Name, options.Instructions, true, tools);
        }
        catch (Exception exception)
        {
            GlobalLogger.Error($"[AI.Agent] 创建会话失败：助手={options.Name}，模型={options.Model}。", exception);
            // IChatClient.Dispose() 释放适配器资源，避免创建失败时遗漏清理。
            chatClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 接入其他实现 IChatClient 的原生适配器。
    /// 非 OpenAI 协议不能只换地址，必须使用对应的统一接口实现。
    /// </summary>
    /// <param name="chatClient">Microsoft.Extensions.AI 的统一聊天客户端。</param>
    /// <param name="name">助手名称。</param>
    /// <param name="instructions">系统提示词，指导助手的回答方式。</param>
    /// <param name="ownsClient">是否随会话释放客户端；共享客户端时保持 false。</param>
    /// <returns>不与其他会话共享历史的助手。</returns>
    public AgentConversation CreateChat(
        IChatClient chatClient,
        string name = "学习助手",
        string instructions = "请用中文回答。",
        bool ownsClient = false,
        IList<AITool>? tools = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // ChatClientAgent 是 Microsoft.Agents.AI 中基于聊天模型的助手实现。
        // chatClient 访问模型，instructions 指定角色，name 指定名称。
        ChatClientAgent agent = new ChatClientAgent(
            chatClient,
            instructions: instructions,
            name: name,
            tools: tools);

        GlobalLogger.DebuggerLogger?.Debug($"[AI.Agent] 使用外部 IChatClient 创建会话：助手={name}。");
        return new AgentConversation(agent, chatClient, ownsClient);
    }
}
