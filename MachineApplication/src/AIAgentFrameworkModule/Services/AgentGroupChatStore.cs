using System.IO;
using System.Text.Json;
using AIAgentFrameworkModule.Models;

namespace AIAgentFrameworkModule.Services;

public sealed class AgentGroupChatStore
{
    public string FilePath { get; }
    public AgentGroupChatStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MachineApplication", "AI", "group-chat.json")) { }
    internal AgentGroupChatStore(string filePath) => FilePath = filePath;

    public GroupChatSettings? Load()
    {
        if (!File.Exists(FilePath)) return null;
        if (new FileInfo(FilePath).Length > 256_000) throw new InvalidDataException("群聊配置文件过大。");
        GroupChatSettings settings = JsonSerializer.Deserialize<GroupChatSettings>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException("群聊配置为空。");
        Validate(settings);
        return settings;
    }

    public void Save(GroupChatSettings settings)
    {
        Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }

    public static void Validate(GroupChatSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RoomName) || settings.RoomName.Length > 60 || settings.Topic is null || settings.Topic.Length > 2000)
            throw new InvalidDataException("群名称必填且不超过 60 字，群约定不超过 2000 字。");
        if (settings.Rounds is < 1 or > 3)
            throw new InvalidDataException("回复轮数为 1–3。");
        if (settings.Members is null || settings.Members.Count is < 1 or > 8 || settings.Members.Any(member => member is null))
            throw new InvalidDataException("请配置 1–8 位 AI 群友。");
        if (settings.Members.Any(member => string.IsNullOrWhiteSpace(member.Name) || member.Name.Length > 24 ||
            member.Name.Trim() == "我" || member.Persona is null || member.Persona.Length > 4000))
            throw new InvalidDataException("群友昵称必填、不超过 24 字且不能为“我”；人设不超过 4000 字。");
        if (settings.Members.Select(member => member.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.Members.Count)
            throw new InvalidDataException("群友昵称不能重复。");
    }
}
