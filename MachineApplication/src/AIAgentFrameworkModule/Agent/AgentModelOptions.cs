namespace AIAgentFrameworkModule.Agent;

/// <summary>一个模型的连接配置。不发请求，不要将真实密钥写入日志或提交到 Git。</summary>
public class AgentModelOptions
{
    /// <summary>助手名称，不是服务器上的模型编号。</summary>
    public string Name { get; set; } = "小A";

    /// <summary>服务商提供的完整模型编号，必须与当前地址和密钥匹配。</summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// API 基础地址，不是网页聊天地址，也不能包含 /chat/completions 或 /responses。
    /// 按服务商文档填写；不自动补 /v1，因为各家路径可能不同。
    /// </summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// 当前服务商的密钥，建议从环境变量读取，不要向陌生中转站发送官方密钥。
    /// 无需鉴权的本机兼容服务可以填 local。
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>接口协议，目前仅支持 Chat Completions，服务商或中转站必须提供此协议。</summary>
    public AgentApiType ApiType { get; set; } = AgentApiType.ChatCompletions;

    /// <summary>系统提示词：指定角色和回答方式，不是安全权限控制。空字符串表示不指定角色。</summary>
    public string Instructions { get; set; } = "你是一位耐心的 C# 老师，请用中文和简单示例回答。";

    /// <summary>单次网络操作的超时秒数，不包含所有重试的总耗时。</summary>
    public int NetworkTimeoutSeconds { get; set; } = 120;

    /// <summary>发送请求前检查配置，并规范化基础地址。</summary>
    /// <returns>以斜杠结尾的绝对地址，方便 SDK 拼接接口路径。</returns>
    public Uri Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("请填写助手名称。", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new ArgumentException("请填写服务商提供的模型编号。", nameof(Model));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentException("请填写密钥；无鉴权的本机服务可以填 local。", nameof(ApiKey));
        }

        if (!Enum.IsDefined(ApiType))
        {
            throw new ArgumentException("不支持的接口协议。", nameof(ApiType));
        }

        if (NetworkTimeoutSeconds <= 0)
        {
            throw new ArgumentException("网络超时必须大于 0 秒。", nameof(NetworkTimeoutSeconds));
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? address))
        {
            throw new ArgumentException("请填写完整的 HTTP 或 HTTPS 基础地址。", nameof(Endpoint));
        }

        // 仅本机开发允许 HTTP，避免远程服务通过明文传输密钥和聊天内容。
        if (address.Scheme != Uri.UriSchemeHttps &&
            !(address.Scheme == Uri.UriSchemeHttp && address.IsLoopback))
        {
            throw new ArgumentException("远程服务必须使用 HTTPS；只有本机服务允许 HTTP。", nameof(Endpoint));
        }

        if (address.Query.Length > 0 || address.Fragment.Length > 0 || address.UserInfo.Length > 0)
        {
            throw new ArgumentException("基础地址不能包含查询参数、片段或用户名密码。", nameof(Endpoint));
        }

        string path = address.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("请填写基础地址，不要包含具体的接口路径。", nameof(Endpoint));
        }

        return new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
    }
}
