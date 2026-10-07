using System.IO;
using System.Text.RegularExpressions;
using Machine.ModuleLoad.Logger.Debugger;
using Xunit;

namespace MachineApplication.Entrance.Tests;

[CollectionDefinition("Console output", DisableParallelization = true)]
public class ConsoleOutputCollection;

[Collection("Console output")]
public class DebuggerLoggerTests
{
    [Fact]
    public void WritesLevelsAndPreservesMessageText()
    {
        var output = Capture(() =>
        {
            var logger = new DebuggerLogger();
            logger.Trace("trace");
            logger.Debug("debug");
            logger.Info("{name}: \u8bbe\u5907");
            logger.Success("success");
            logger.Warn("warn");
            logger.Error("error");
            logger.Fatal("fatal");
        });

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(7, lines.Length);
        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[TRACE  \] trace$", lines[0]);
        Assert.EndsWith("[DEBUG  ] debug", lines[1]);
        Assert.EndsWith("[INFO   ] {name}: \u8bbe\u5907", lines[2]);
        Assert.EndsWith("[SUCCESS] success", lines[3]);
        Assert.EndsWith("[WARN   ] warn", lines[4]);
        Assert.EndsWith("[ERROR  ] error", lines[5]);
        Assert.EndsWith("[FATAL  ] fatal", lines[6]);
    }

    [Fact]
    public void IncludesExceptionStackAndInnerException()
    {
        var exception = Assert.Throws<InvalidOperationException>((Action)(() =>
            throw new InvalidOperationException("outer", new IOException("inner"))));
        var output = Capture(() => new DebuggerLogger().Error("operation failed", exception));
        Assert.Contains("[ERROR  ] operation failed", output);
        Assert.Contains(exception.ToString(), output);
        Assert.Contains(nameof(IncludesExceptionStackAndInnerException), output);
    }

    [Fact]
    public void KeepsMultilineEntriesTogetherAcrossInstances()
    {
        var output = Capture(() => Parallel.For(0, 100, index =>
            new DebuggerLogger().Info($"start:{index}{Environment.NewLine}end:{index}")));
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(200, lines.Length);
        for (var index = 0; index < lines.Length; index += 2)
        {
            var id = lines[index].Substring(lines[index].LastIndexOf(':') + 1);
            Assert.Equal("end:" + id, lines[index + 1]);
        }
    }

    [Fact]
    public void UsesAnsiColorsAndResetsEachEntryWhenRedirected()
    {
        var output = Capture(() =>
        {
            var logger = new DebuggerLogger();
            logger.Trace("trace");
            logger.Debug("debug");
            logger.Info("info");
            logger.Success("success");
            logger.Banner("banner");
            logger.Warn("warn");
            logger.Error("error");
            logger.Fatal("fatal");
        }, stripAnsi: false);

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(8, lines.Length);
        if (Console.IsOutputRedirected)
        {
            string[] colors = ["128;128;128", "0;255;255", "255;255;255", "50;205;50",
                "255;105;180", "255;255;0", "255;0;0", "255;0;255"];
            for (var index = 0; index < lines.Length; index++)
            {
                Assert.StartsWith($"\u001b[38;2;{colors[index]}m", lines[index]);
                Assert.EndsWith("\u001b[0m", lines[index]);
            }
        }
        else
        {
            Assert.DoesNotContain("\u001b[", output);
        }
    }

    private static string Capture(Action action, bool stripAnsi = true)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            action();
            var output = writer.ToString();
            return stripAnsi ? Regex.Replace(output, @"\x1B\[[0-9;]*m", "") : output;
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
