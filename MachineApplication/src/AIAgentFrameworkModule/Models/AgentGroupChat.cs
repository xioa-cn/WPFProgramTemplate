using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAgentFrameworkModule.Models;

public sealed class AgentGroupMember : ObservableObject
{
    private string name = "新群友";
    private string persona = "自然、简短地参与群聊，回应话题，也可以接其他群友的话。";
    private AgentModelProfile? profile;
    private bool enabled = true;
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string Persona { get => persona; set => SetProperty(ref persona, value); }
    public AgentModelProfile? Profile { get => profile; set => SetProperty(ref profile, value); }
    public bool Enabled { get => enabled; set => SetProperty(ref enabled, value); }
    internal GroupMemberSettings? SavedReference { get; set; }
}

public sealed class GroupChatMessage : ObservableObject
{
    private string text = "";
    private string status = "";
    private bool generating;
    private bool showTimestamp;
    public string Speaker { get; init; } = "";
    public string Model { get; init; } = "";
    public bool IsUser { get; init; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public bool ShowTimestamp { get => showTimestamp; set => SetProperty(ref showTimestamp, value); }
    public string Text { get => text; set => SetProperty(ref text, value); }
    public string Status { get => status; set => SetProperty(ref status, value); }
    public bool IsGenerating { get => generating; set => SetProperty(ref generating, value); }
}

public sealed class GroupChatSettings
{
    public string RoomName { get; set; } = "AI 群友聊天室";
    public string Topic { get; set; } = "轻松交流，认真回应；有不同意见也可以友善讨论。";
    public int Rounds { get; set; } = 1;
    public List<GroupMemberSettings> Members { get; set; } = [];
}

public sealed class GroupMemberSettings
{
    public string Name { get; set; } = "";
    public string Persona { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string ProfileName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
}
