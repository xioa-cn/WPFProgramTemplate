using System.IO;
using System.Text;
using System.Text.Json;
using AIAgentFrameworkModule.Models;
using Machine.ModuleLoad.Logger;

namespace AIAgentFrameworkModule.Services;

/// <summary>保存非密钥配置到当前 Windows 用户目录，不保存聊天记录和直接输入的密钥。</summary>
public class AgentProfileStore
{
    /// <summary>配置路径独立于程序安装目录，调试和发布都不需要写入源码目录。</summary>
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MachineApplication", "AI", "models.json");

    /// <summary>读取已保存配置；不存在时返回空列表，损坏时交给页面明确提示。</summary>
    public List<AgentModelProfile> Load()
    {
        if (!File.Exists(FilePath))
        {
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 未找到模型配置文件，使用空配置：{FilePath}。");
            return new List<AgentModelProfile>();
        }

        // JsonSerializer 是 .NET JSON 工具，ApiKey 的 JsonIgnore 标记会阻止密钥读写。
        string json = File.ReadAllText(FilePath, Encoding.UTF8);
        List<AgentModelProfile>? profiles = JsonSerializer.Deserialize<List<AgentModelProfile>>(json);
        if (profiles == null || profiles.Count > 100 || profiles.Any(profile => profile == null))
        {
            throw new InvalidDataException("模型配置文件格式不正确，或配置数量超过 100。");
        }

        GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 已读取 {profiles.Count} 份模型配置：{FilePath}。");
        return profiles;
    }

    /// <summary>保存全部配置，用同目录临时文件替换，降低中途失败导致原文件损坏的风险。</summary>
    public void Save(IEnumerable<AgentModelProfile> profiles)
    {
        List<AgentModelProfile> items = profiles.ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        JsonSerializerOptions settings = new JsonSerializerOptions();
        settings.WriteIndented = true;
        string json = JsonSerializer.Serialize(items, settings);
        string temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
        File.Move(temporaryPath, FilePath, true);
        GlobalLogger.DebuggerLogger?.Debug($"[AI.Config] 已保存 {items.Count} 份模型配置：{FilePath}。");
    }
}
