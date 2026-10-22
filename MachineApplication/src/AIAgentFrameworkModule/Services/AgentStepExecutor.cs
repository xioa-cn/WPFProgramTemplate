using System.Runtime.CompilerServices;
using AIAgentFrameworkModule.Agent;
using Microsoft.Extensions.AI;

namespace AIAgentFrameworkModule.Services;

public interface IAgentStepExecutor
{
    IAsyncEnumerable<string> ExecuteAsync(AgentModelOptions options, string prompt, CancellationToken cancellationToken);

    IAsyncEnumerable<string> ExecuteWithToolsAsync(AgentModelOptions options, string prompt,
        IList<AITool> tools, CancellationToken cancellationToken)
        => throw new NotSupportedException("此执行器不支持工具调用。");
}

public sealed class AgentStepExecutor(AgentCore core) : IAgentStepExecutor
{
    public IAsyncEnumerable<string> ExecuteWithToolsAsync(AgentModelOptions options, string prompt,
        IList<AITool> tools, CancellationToken cancellationToken)
        => ExecuteCoreAsync(options, prompt, tools, cancellationToken);

    public IAsyncEnumerable<string> ExecuteAsync(AgentModelOptions options, string prompt, CancellationToken cancellationToken)
        => ExecuteCoreAsync(options, prompt, null, cancellationToken);

    private async IAsyncEnumerable<string> ExecuteCoreAsync(
        AgentModelOptions options, string prompt, IList<AITool>? tools, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using AgentConversation conversation = core.CreateChat(options, tools);
        await foreach (string part in conversation.AskStreamingAsync(prompt, cancellationToken).ConfigureAwait(false))
            yield return part;
    }
}
