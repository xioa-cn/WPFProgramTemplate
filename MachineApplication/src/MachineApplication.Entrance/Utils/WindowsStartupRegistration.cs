using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MachineApplication.Entrance.Utils;

public sealed class WindowsStartupRegistration {
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _registryPath;

    public string ValueName { get; }
    public string Command { get; }

    public WindowsStartupRegistration(string executablePath, string registryPath = RunKeyPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        var fullPath = Path.GetFullPath(executablePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains('"'))
            throw new ArgumentException("An executable (.exe) path is required.", nameof(executablePath));
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())));
        ValueName = $"MachineApplication.{identifier}";
        Command = "\"" + fullPath + "\"";
        _registryPath = registryPath;
    }

    public bool IsEnabled() {
        using var key = Registry.CurrentUser.OpenSubKey(_registryPath);
        return string.Equals(key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string,
            Command, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled) {
        if (enabled) {
            if (Command.Length > 260)
                throw new InvalidOperationException("The startup command exceeds the Windows Run key limit of 260 characters.");
            if (string.Equals(Path.GetFileNameWithoutExtension(Command.Trim('"')), "dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Launch the application executable instead of dotnet to enable startup.");
            using var key = Registry.CurrentUser.CreateSubKey(_registryPath, writable: true);
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else {
            using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
