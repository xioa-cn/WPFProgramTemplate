using System.IO;
using System.Xml.Linq;
using MachineApplication.Entrance.Utils;
using MachineApplication.Entrance.ViewModels;
using Microsoft.Win32;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class WindowsStartupRegistrationTests {
    [Fact]
    public void CommandQuotesExecutablePathsContainingSpacesAndChinese() {
        var executable = Path.Combine(Path.GetTempPath(), "测试程序 folder", "Client.exe");
        var registration = new WindowsStartupRegistration(executable);

        Assert.Equal("\"" + executable + "\"", registration.Command);
    }

    [Fact]
    public void ApplicationIdentityUsesFullPathAndIgnoresCase() {
        var firstPath = Path.Combine(Path.GetTempPath(), "AppOne", "Client.exe");
        var secondPath = Path.Combine(Path.GetTempPath(), "AppTwo", "Client.exe");

        Assert.NotEqual(new WindowsStartupRegistration(firstPath).ValueName,
            new WindowsStartupRegistration(secondPath).ValueName);
        Assert.Equal(new WindowsStartupRegistration(firstPath).ValueName,
            new WindowsStartupRegistration(firstPath.ToUpperInvariant()).ValueName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Client.dll")]
    public void InvalidExecutableIsRejected(string path) {
        Assert.ThrowsAny<ArgumentException>(() => new WindowsStartupRegistration(path));
    }

    [Fact]
    public void EnableAndDisableOnlyChangeThisApplicationsValue() {
        var registryPath = $@"Software\MachineApplication.Tests\Startup.{Guid.NewGuid():N}";
        var registration = new WindowsStartupRegistration(
            Path.Combine(Path.GetTempPath(), "测试应用", "Client.exe"), registryPath);
        try {
            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                key.SetValue("OtherApplication", "untouched");

            Assert.False(registration.IsEnabled());
            registration.SetEnabled(true);
            registration.SetEnabled(true);
            Assert.True(registration.IsEnabled());
            using (var key = Registry.CurrentUser.OpenSubKey(registryPath)) {
                Assert.Equal(registration.Command, key!.GetValue(registration.ValueName));
                Assert.Equal(RegistryValueKind.String, key.GetValueKind(registration.ValueName));
            }

            registration.SetEnabled(false);
            registration.SetEnabled(false);
            Assert.False(registration.IsEnabled());
            using var remaining = Registry.CurrentUser.OpenSubKey(registryPath);
            Assert.Equal("untouched", remaining!.GetValue("OtherApplication"));
            Assert.Null(remaining.GetValue(registration.ValueName));
        }
        finally {
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void DisablingMissingRegistrationDoesNotCreateRegistryKey() {
        var registryPath = $@"Software\MachineApplication.Tests\Startup.{Guid.NewGuid():N}";
        var registration = new WindowsStartupRegistration(
            Path.Combine(Path.GetTempPath(), "Client.exe"), registryPath);

        registration.SetEnabled(false);

        Assert.False(registration.IsEnabled());
        using var key = Registry.CurrentUser.OpenSubKey(registryPath);
        Assert.Null(key);
    }

    [Fact]
    public void OverlongCommandAndDotnetHostAreNotRegistered() {
        var registryPath = $@"Software\MachineApplication.Tests\Startup.{Guid.NewGuid():N}";
        var longPath = Path.Combine(Path.GetTempPath(), new string('a', 220), "Client.exe");
        var longRegistration = new WindowsStartupRegistration(longPath, registryPath);
        var dotnetRegistration = new WindowsStartupRegistration(
            Path.Combine(Path.GetTempPath(), "dotnet.exe"), registryPath);

        if (longRegistration.Command.Length <= 260)
            longRegistration = new WindowsStartupRegistration(
                Path.Combine(Path.GetTempPath(), new string('a', 250), "Client.exe"), registryPath);
        Assert.Throws<InvalidOperationException>(() => longRegistration.SetEnabled(true));
        Assert.Throws<InvalidOperationException>(() => dotnetRegistration.SetEnabled(true));
        using var key = Registry.CurrentUser.OpenSubKey(registryPath);
        Assert.Null(key);
    }

    [Fact]
    public void SettingsPageContainsTwoWayStartupCheckbox() {
        var pagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/MachineApplication.Entrance/Views/SuperPage.xaml"));
        var page = XDocument.Load(pagePath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var checkbox = page.Descendants(presentation + "CheckBox").Single(control =>
            ((string?)control.Attribute("IsChecked"))?.Contains(nameof(SuperViewModel.StartWithWindows)) == true);
        Assert.Equal("{Binding StartWithWindows, Mode=TwoWay}", (string?)checkbox.Attribute("IsChecked"));
        Assert.Equal("{Binding IsLoaded}", (string?)checkbox.Attribute("IsEnabled"));
        Assert.NotNull(typeof(SuperViewModel).GetProperty(nameof(SuperViewModel.StartWithWindows)));
    }
}
