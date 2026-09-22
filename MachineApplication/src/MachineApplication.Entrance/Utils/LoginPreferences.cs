using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MachineApplication.Entrance.Utils;

/// <summary>仅为当前 Windows 用户保存加密登录凭据。</summary>
internal sealed class LoginPreferences
{
    public string Account { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public bool AutoLogin { get; set; }
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MachineApplication", "login.json");

    public static LoginPreferences Load()
    {
        try { return JsonSerializer.Deserialize<LoginPreferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public string ReadPassword()
    {
        if (string.IsNullOrEmpty(ProtectedPassword)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword), null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch { return ""; }
    }

    public static void Save(string account, string password, bool remember, bool automatic)
    {
        var settings = new LoginPreferences { Account = account, AutoLogin = remember && automatic };
        if (remember)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            try { settings.ProtectedPassword = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
        File.Move(temporary, FilePath, true);
    }
}
