using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAgentFrameworkModule.Models;

/// <summary>一条仅用于界面显示的消息，真正发给模型的历史由 AgentConversation 管理。</summary>
public class AgentChatMessage : ObservableObject
{
    private string text = "";
    private bool isGenerating;

    /// <summary>消息发送者，例如“你”或“助手”。</summary>
    public string Role { get; }

    /// <summary>本机显示时间，不用于服务端排序。</summary>
    public string Time { get; } = DateTime.Now.ToString("HH:mm:ss");

    /// <summary>文本内容；流式追加时通过 SetProperty 通知 WPF 刷新。</summary>
    public string Text
    {
        get => text;
        set
        {
            bool wasWaiting = IsWaiting;
            if (SetProperty(ref text, value) && wasWaiting != IsWaiting) OnPropertyChanged(nameof(IsWaiting));
        }
    }

    /// <summary>正在等待或接收此消息；不是模型内部推理内容。</summary>
    public bool IsGenerating
    {
        get => isGenerating;
        set
        {
            if (SetProperty(ref isGenerating, value)) OnPropertyChanged(nameof(IsWaiting));
        }
    }

    /// <summary>尚未收到文字时显示“正在思考”，第一段文字到达后立即隐藏。</summary>
    public bool IsWaiting => IsGenerating && string.IsNullOrEmpty(Text);

    /// <summary>创建显示消息，不会发送网络请求。</summary>
    public AgentChatMessage(string role, string text)
    {
        Role = role;
        this.text = text;
    }
}
