using System.IO;
using Machine.ModuleLoad.Utils;
using MachineApplication.Entrance.Theme;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class JsonFileUtilsTests
{
    [Fact]
    public void JsonFailuresReturnErrorsAndPreservePreviousFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MachineJsonTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "Theme.json");
            Assert.True(JsonFileUtils.Read<ThemeSettings>(path).IsErr);
            Assert.Equal(new ThemeSettings(), JsonFileUtils.Read(path, () => new ThemeSettings()).Unwrap());
            Assert.True(JsonFileUtils.Write(path, new ThemeSettings()).IsOk);
            var previous = File.ReadAllText(path);
            Assert.True(JsonFileUtils.Write(path, new { Ratio = double.NaN }).IsErr);
            Assert.Equal(previous, File.ReadAllText(path));
            // 目录不能作为 JSON 文件替换；必须返回错误并清理临时文件。
            Assert.True(JsonFileUtils.Write(directory, new ThemeSettings()).IsErr);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            File.WriteAllText(path, "null");
            Assert.True(JsonFileUtils.Read<ThemeSettings>(path).IsErr);
            File.WriteAllText(path, "{ invalid");
            Assert.True(JsonFileUtils.Read(path, () => new ThemeSettings()).IsErr);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ThemeSettingsRejectInvalidValuesWithoutThrowing()
    {
        Assert.True((new ThemeSettings { PrimaryColor = "invalid" }).Validate().IsErr);
        Assert.True((new ThemeSettings { SecondaryColor = null! }).Validate().IsErr);
        Assert.True((new ThemeSettings { DesiredContrastRatio = 22 }).Validate().IsErr);
        Assert.True((new ThemeSettings { DesiredContrastRatio = double.NaN }).Validate().IsErr);
        Assert.True((new ThemeSettings { Contrast = (Contrast)999 }).Validate().IsErr);
        Assert.True((new ThemeSettings { PrimaryColor = "#123456" }).Validate().IsOk);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Theme.json"), new ThemeSettingsService().FilePath);
    }
}
