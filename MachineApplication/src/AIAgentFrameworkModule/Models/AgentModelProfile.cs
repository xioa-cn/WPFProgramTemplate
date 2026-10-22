using System.Text.Json.Serialization;
using AIAgentFrameworkModule.Agent;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAgentFrameworkModule.Models;

/// <summary>页面上的一份模型配置。普通配置可保存，直接输入的密钥只留在内存。</summary>
public class AgentModelProfile : ObservableObject
{
    private string name = "新模型";
    private string endpoint = "https://api.openai.com/v1";
    private string model = "";
    private string apiKey = "";
    private string apiKeyEnvironmentVariable = "AI_API_KEY";
    private string instructions = "你是一位耐心的 C# 老师，请用中文和简单示例回答。";
    private string timeoutSeconds = "120";

    /// <summary>配置名称；SetProperty 来自 MVVM Toolkit，赋值后自动通知界面刷新。</summary>
    public string Name { get => name; set => SetProperty(ref name, value); }

    /// <summary>官方或中转服务的 API 基础地址，不含 /chat/completions。</summary>
    public string Endpoint { get => endpoint; set => SetProperty(ref endpoint, value); }

    /// <summary>当前服务提供的模型编号。</summary>
    public string Model { get => model; set => SetProperty(ref model, value); }

    /// <summary>本次运行使用的密钥；JsonIgnore 明确禁止 JSON 序列化和反序列化这个属性。</summary>
    [JsonIgnore]
    public string ApiKey { get => apiKey; set => SetProperty(ref apiKey, value); }

    /// <summary>直接输入的密钥为空时，按进程、当前 Windows 用户、系统的顺序读取变量；文件仅保存变量名。</summary>
    public string ApiKeyEnvironmentVariable
    {
        get => apiKeyEnvironmentVariable;
        set => SetProperty(ref apiKeyEnvironmentVariable, value);
    }

    /// <summary>发给助手的角色说明，不是用户问题。</summary>
    public string Instructions { get => instructions; set => SetProperty(ref instructions, value); }

    /// <summary>保留为文字，便于明确提示输入错误，而不是让 WPF 静默拒绝非法数字。</summary>
    public string TimeoutSeconds { get => timeoutSeconds; set => SetProperty(ref timeoutSeconds, value); }

    /// <summary>把界面数据转换为独立的连接配置快照，后续编辑不会修改已经创建的会话。</summary>
    public AgentModelOptions CreateOptions()
    {
        if (!int.TryParse(TimeoutSeconds, out int seconds) || seconds <= 0)
        {
            throw new ArgumentException("超时秒数必须是大于 0 的整数。");
        }

        string key = ResolveApiKey();

        AgentModelOptions options = new AgentModelOptions();
        options.Name = Name?.Trim() ?? "";
        options.Endpoint = Endpoint?.Trim() ?? "";
        options.Model = Model?.Trim() ?? "";
        options.ApiKey = key?.Trim() ?? "";
        options.Instructions = Instructions ?? "";
        options.NetworkTimeoutSeconds = seconds;
        options.ApiType = AgentApiType.ChatCompletions;
        options.Validate();
        return options;
    }

    /// <summary>
    /// 读取密钥，不会把环境变量输入框中的文字直接当作密钥。
    /// 直接 Key 优先，其次是进程变量、用户变量、系统变量；不记录或显示读取到的值。
    /// </summary>
    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey)) return ApiKey.Trim();

        string variableName = ApiKeyEnvironmentVariable?.Trim() ?? "";
        if (variableName.Length == 0)
        {
            throw new ArgumentException("请在 API Key 中输入密钥，或填写已经在 Windows 中设置好的环境变量名称，两种方式任选一种。");
        }

        if (variableName.Contains('=') || variableName.Contains('\0') ||
            variableName.Contains('\r') || variableName.Contains('\n') ||
            variableName.Contains('%') || variableName.StartsWith("$", StringComparison.Ordinal))
        {
            throw new ArgumentException("这里只填写环境变量名称，例如 DEMOKEY，不要填写 DEMOKEY=密钥、%DEMOKEY% 或 $env:DEMOKEY。");
        }

        try
        {
            // Environment.GetEnvironmentVariable 是 .NET 方法，不是 AI SDK 方法。
            // Process 读取程序启动时继承的变量，也支持启动程序的终端临时设置的变量。
            string? key = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process);

            // User 直接读取当前 Windows 用户保存的变量，补足程序启动后新建变量无法被进程继承的情况。
            if (string.IsNullOrWhiteSpace(key))
                key = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);

            // Machine 读取 Windows 系统级变量；只在前两级没有非空值时使用。
            if (string.IsNullOrWhiteSpace(key))
                key = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);

            if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
        }
        catch (System.Security.SecurityException)
        {
            throw new ArgumentException("没有权限读取指定的环境变量，请检查 Windows 权限，或改在 API Key 输入框中直接填写密钥。");
        }
        catch (UnauthorizedAccessException)
        {
            throw new ArgumentException("环境变量读取被拒绝，请检查 Windows 权限，或改在 API Key 输入框中直接填写密钥。");
        }

        // 不回显变量值，也不把失败伪装成必须再次填写直接 Key。
        throw new ArgumentException("未从进程、当前 Windows 用户或系统环境变量中读取到非空密钥。此处填写的是变量名，不是密钥；请先在 Windows 环境变量中创建该名称并将值设为真实密钥，或直接填写上方 API Key。");
    }
}
