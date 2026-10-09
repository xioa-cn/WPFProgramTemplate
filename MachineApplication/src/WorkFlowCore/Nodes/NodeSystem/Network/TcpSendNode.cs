using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class TcpSendNode : SystemNode
{
    private TcpConnectionRegistry? _registry;

    protected TcpSendNode(string title) : base(title, typeof(int))
    {
        SendInput = AddInput("发送数据", () => SendText);
        Output.Text = "发送字节数";
    }

    [XTNodeProperty("连接 Key", "同一画布内对应连接节点的 Key，不区分大小写；连接需已启动。")]
    public string Key { get; set; } = "tcp1";

    [XTNodeProperty("发送文本", "未连接发送端口时使用 UTF-8 文本；端口接受 byte[]，不会自动追加换行。")]
    public string SendText { get; set; } = string.Empty;

    [XTNodeProperty("发送超时（毫秒）", "包含等待发送锁的超时；失败不自动重发，避免重复发送。")]
    public int TimeoutMilliseconds { get; set; } = 5000;

    public XTNodeOption SendInput { get; }
    protected abstract bool IsServer { get; }
    protected virtual string GetClientId(object?[] values) => string.Empty;

    protected override void ValidateSettings()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        if (TimeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMilliseconds));
    }

    public override EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        VerifyAccess();
        _registry = Owner is null ? null : TcpConnectionRegistry.For(Owner);
        return base.Execute(context);
    }

    protected override async Task<object?> RunAsync(object?[] values, CancellationToken cancellationToken)
    {
        var connection = (_registry ?? throw new InvalidOperationException("请先将发送节点添加到画布。"))
            .Get(Key.Trim(), IsServer);
        var payload = values[0] is byte[] bytes ? bytes.ToArray()
            : SystemValues.Encoding(TextEncodingKind.Utf8).GetBytes(SystemValues.Text(values[0]));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeoutMilliseconds);
        await connection.SendAsync(payload, GetClientId(values), timeout.Token).ConfigureAwait(false);
        return payload.Length;
    }
}
