using System.Diagnostics;
using System.Runtime.CompilerServices;
using Machine.ModuleLoad.Logger;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AIAgentFrameworkModule.Agent;

/// <summary>
/// 独立对话，支持普通回答、流式回答和重置上下文，不自动保存到文件或数据库。
/// 同一实例的操作会排队，避免同时修改历史；不同实例可以同时聊天。
/// </summary>
public sealed class AgentConversation : IDisposable
{
    // AIAgent 是微软框架的助手基类，提供运行助手和创建会话的方法。
    private readonly AIAgent agent;

    // IChatClient 是统一模型适配器，保存它以便在我们拥有它时释放资源。
    private readonly IChatClient chatClient;
    private readonly bool ownsClient;

    // AgentSession 是微软的会话状态，复用它即可连续聊天，不是模型的永久记忆。
    private AgentSession? session;

    // SemaphoreSlim 是 .NET 的异步锁，保证同一会话一次只处理一个操作。
    private readonly SemaphoreSlim operationLock = new SemaphoreSlim(1, 1);
    private bool disposed;

    /// <summary>由 AgentCore 创建，绑定助手、适配器及资源释放责任。</summary>
    /// <param name="agent">微软框架的助手对象。</param>
    /// <param name="chatClient">底层模型适配器。</param>
    /// <param name="ownsClient">是否由本会话释放客户端。</param>
    internal AgentConversation(AIAgent agent, IChatClient chatClient, bool ownsClient)
    {
        this.agent = agent;
        this.chatClient = chatClient;
        this.ownsClient = ownsClient;
    }

    /// <summary>发送问题，等待完整回答；后续调用带上本会话的上下文。</summary>
    /// <param name="message">用户问题，不允许空白。</param>
    /// <param name="cancellationToken">取消令牌，可中断等待或模型请求。</param>
    /// <returns>回答的文本，非文本响应可能返回空字符串。</returns>
    public async Task<string> AskAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        await operationLock.WaitAsync(cancellationToken);
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            AgentSession currentSession = await GetSessionAsync(cancellationToken);
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Chat] 发送问题：字符数={message.Length}。");

            // RunAsync 发送问题并更新会话；AgentResponse 是完整结果，可以包含多条消息。
            AgentResponse response = await agent.RunAsync(
                message, currentSession, cancellationToken: cancellationToken);

            // AgentResponse.Text 合并结果中的文本，不包含图片等非文本内容。
            string answer = response.Text;
            GlobalLogger.DebuggerLogger?.Debug(
                $"[AI.Chat] 收到完整回答：字符数={answer.Length}，耗时 {timer.Elapsed.TotalSeconds.ToString("F1")} 秒。");
            return answer;
        }
        catch (OperationCanceledException)
        {
            GlobalLogger.Warn($"[AI.Chat] 请求已取消，耗时 {timer.Elapsed.TotalSeconds.ToString("F1")} 秒。");
            throw;
        }
        catch (Exception exception)
        {
            GlobalLogger.Error("[AI.Chat] 请求失败。", exception);
            throw;
        }
        finally
        {
            // 成功、异常、取消都要归还锁，否则后续提问会一直等待。
            operationLock.Release();
        }
    }

    /// <summary>
    /// 逐段返回回答，使用 await foreach 接收，适合界面逐步显示。
    /// 提前 break、取消或异常不保证历史完整，需要时先 ResetAsync 再继续。
    /// </summary>
    /// <param name="message">用户问题。</param>
    /// <param name="cancellationToken">取消令牌，EnumeratorCancellation 让异步枚举的取消也生效。</param>
    /// <returns>本次新增的文本片段，不是全部回答。</returns>
    public async IAsyncEnumerable<string> AskStreamingAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        await operationLock.WaitAsync(cancellationToken);
        int partCount = 0;
        bool completed = false;
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            AgentSession currentSession = await GetSessionAsync(cancellationToken);
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Chat] 开始流式请求：字符数={message.Length}。");

            // RunStreamingAsync 返回异步更新流；await foreach 等待片段而不阻塞界面。
            // AgentResponseUpdate 是一段更新，可能只含状态、用量而没有文字。
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
                message, currentSession, cancellationToken: cancellationToken))
            {
                // Text 只取当前更新中的文字；过滤空更新后交给调用者拼接显示。
                if (!string.IsNullOrEmpty(update.Text))
                {
                    partCount++;
                    yield return update.Text;
                }
            }

            completed = true;
        }
        finally
        {
            string seconds = timer.Elapsed.TotalSeconds.ToString("F1");
            if (completed)
                GlobalLogger.DebuggerLogger?.Debug(
                    $"[AI.Chat] 流式回答结束：{partCount} 个片段，耗时 {seconds} 秒。");
            else if (cancellationToken.IsCancellationRequested)
                GlobalLogger.Warn($"[AI.Chat] 流式请求已取消：已收到 {partCount} 个片段，耗时 {seconds} 秒。");
            else
                GlobalLogger.DebuggerLogger?.Debug(
                    $"[AI.Chat] 流式枚举提前结束：已收到 {partCount} 个片段，耗时 {seconds} 秒。");
            operationLock.Release();
        }
    }

    /// <summary>
    /// 丢弃本地会话，开始新对话，不会删除服务商保存的记录。
    /// 正在回答时会等待；想立即停止，应先取消那次请求。
    /// </summary>
    /// <param name="cancellationToken">取消等待的令牌。</param>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            session = null;
            GlobalLogger.DebuggerLogger?.Debug("[AI.Chat] 已丢弃本地会话上下文，下一次提问将创建新会话。");
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>首次聊天时创建 AgentSession，之后复用它维护上下文。</summary>
    /// <param name="cancellationToken">传给微软会话创建方法的取消令牌。</param>
    /// <returns>与当前助手兼容的会话对象。</returns>
    private async Task<AgentSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (session == null)
        {
            // CreateSessionAsync 创建与 agent 兼容的空会话，不用手动拼装内部状态。
            GlobalLogger.DebuggerLogger?.Debug("[AI.Chat] 创建新的 AgentSession（首次提问或上下文已重置）。");
            session = await agent.CreateSessionAsync(cancellationToken);
        }

        return session;
    }

    /// <summary>
    /// 释放本会话拥有的客户端，先取消并等待正在进行的请求结束再调用。
    /// 外部传入且 ownsClient 为 false 的共享客户端由调用者释放。
    /// </summary>
    public void Dispose()
    {
        // 不在 UI 线程同步等待网络请求，避免界面死锁。
        if (!operationLock.Wait(0))
        {
            throw new InvalidOperationException("请先取消并等待聊天结束，再释放会话。");
        }

        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            session = null;
            GlobalLogger.DebuggerLogger?.Debug($"[AI.Chat] 释放会话；模型客户端由本会话释放={ownsClient}。");
            if (ownsClient)
            {
                // IChatClient.Dispose 释放当前会话独占的模型适配器。
                chatClient.Dispose();
            }
        }
        finally
        {
            // 保留轻量锁，让已排队的调用能够醒来并收到已释放异常。
            operationLock.Release();
        }
    }
}
