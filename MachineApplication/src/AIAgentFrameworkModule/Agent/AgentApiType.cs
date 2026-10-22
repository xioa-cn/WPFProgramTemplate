namespace AIAgentFrameworkModule.Agent;

/// <summary>接口协议，不是模型品牌。目前仅接入非实验性的 Chat Completions API。</summary>
public enum AgentApiType
{
    /// <summary>聊天补全协议，SDK 在基础地址后添加 /chat/completions。</summary>
    ChatCompletions
}
