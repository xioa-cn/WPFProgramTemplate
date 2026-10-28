using System.IO.Pipes;
using System.Reflection;
using Machine.ModuleLoad;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class SingleInstanceChannelTests {
    private static readonly Type ChannelType = typeof(AppMultipleAccounts).Assembly
        .GetType("Machine.ModuleLoad.SingleInstanceChannel", throwOnError: true)!;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplicationIdMustNotBeBlank(string? applicationId) {
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().MultipleAccounts(applicationId!));
    }

    [Fact]
    public async Task RepeatedNotificationsActivateTheExistingInstance() {
        var pipeName = $"MachineApplication.Tests.{Guid.NewGuid():N}";
        var activations = 0;
        using var channel = CreateChannel(pipeName, () => Interlocked.Increment(ref activations));

        for (var attempt = 0; attempt < 3; attempt++)
            Assert.True(await NotifyAsync(pipeName));

        Assert.Equal(3, Volatile.Read(ref activations));
    }

    [Fact]
    public async Task DisconnectedClientDoesNotStopTheListener() {
        var pipeName = $"MachineApplication.Tests.{Guid.NewGuid():N}";
        var activations = 0;
        using var channel = CreateChannel(pipeName, () => Interlocked.Increment(ref activations));
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5))) {
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(timeout.Token);
            var processId = new byte[sizeof(int)];
            await client.ReadExactlyAsync(processId, timeout.Token);
        }

        Assert.True(await NotifyAsync(pipeName));
        Assert.Equal(1, Volatile.Read(ref activations));
    }

    [Fact]
    public async Task DifferentApplicationChannelsDoNotActivateEachOther() {
        var firstName = $"MachineApplication.Tests.{Guid.NewGuid():N}";
        var secondName = $"MachineApplication.Tests.{Guid.NewGuid():N}";
        var firstActivations = 0;
        var secondActivations = 0;
        using var first = CreateChannel(firstName, () => Interlocked.Increment(ref firstActivations));
        using var second = CreateChannel(secondName, () => Interlocked.Increment(ref secondActivations));

        Assert.True(await NotifyAsync(firstName));
        Assert.Equal(1, Volatile.Read(ref firstActivations));
        Assert.Equal(0, Volatile.Read(ref secondActivations));
    }

    [Fact]
    public async Task DisposingListenerReleasesItsPipeName() {
        var pipeName = $"MachineApplication.Tests.{Guid.NewGuid():N}";
        var original = CreateChannel(pipeName, () => { });
        original.Dispose();
        original.Dispose();
        using var replacement = CreateChannel(pipeName, () => { });

        Assert.True(await NotifyAsync(pipeName));
    }

    [Fact]
    public async Task MissingInstanceTimesOutWithoutShowingADialog() {
        Assert.False(await NotifyAsync($"MachineApplication.Tests.{Guid.NewGuid():N}"));
    }

    private static IDisposable CreateChannel(string pipeName, Action activate) =>
        (IDisposable)Activator.CreateInstance(ChannelType, pipeName, activate)!;

    private static Task<bool> NotifyAsync(string pipeName) =>
        (Task<bool>)ChannelType.GetMethod("NotifyAsync", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object[] { pipeName })!;
}
