using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("TCP 服务端")]
[XTNode("网络通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "启动后持续后台监听，支持多个客户端及同连接多次请求。手动停止、取消启动上下文或移除节点时关闭。")]
public sealed class TcpServerNode : TcpAcceptNode, IEditorCustomEditorNode
{
    private PersistentTcpServer? _server;
    private CancellationTokenRegistration _cancellation;
    private Task _stopping = Task.CompletedTask;

    public TcpServerNode() : base("TCP 服务端")
    {
        ReceiveBytes = 1;
        Output.Text = "收到数据";
        Completed.Text = "收到请求";
        Output.Description = "每次完整请求的 byte[]；启动成功不广播虚假接收数据。";
        Unloaded += (_, _) => StopServer();
    }

    [XTNodeProperty("最大客户端数", "同时保持连接的客户端上限，1 到 1024；超出时关闭新连接。")]
    public int MaxClients { get; set; } = 32;

    public bool IsListening => _server?.IsListening == true;
    public string EditorButtonText => "启动 / 停止 TCP 服务端";

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        if (MaxClients is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaxClients));
    }

    public override EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var values = ReadInputs(false)!;
            ValidateValues(values);
            var settings = new TcpServerSettings(LocalAddress(BindAddress), Port, ReceiveBytes, MaxResponseBytes, TimeoutMilliseconds, MaxClients);
            var reply = Payload(values[0]);
            if (IsListening)
            {
                if (_server!.Settings != settings || !_server.Reply.AsSpan().SequenceEqual(reply))
                    return EditorNodeExecutionResult.Failure("服务端正在运行；修改配置后请先停止，再重新启动。");
                return EditorNodeExecutionResult.Success("TCP 服务端已在监听，不重复启动。");
            }
            StopServer();
            Output.Data = null;
            Completed.Data = null;
            PersistentTcpServer? server = null;
            server = new PersistentTcpServer(settings, reply,
                (bytes, token) => Dispatcher.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(_server, server) || !server.IsListening) return;
                    Output.TransferData(bytes);
                    Completed.TransferData(new EditorFlowSignal(context.ExecutionId));
                    RuntimeText = $"监听中 · 收到 {bytes.Length} 字节";
                }, DispatcherPriority.Background, token).Task,
                (error, token) => Dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(_server, server)) RuntimeText = error;
                }, DispatcherPriority.Background, token).Task);
            server.Start();
            _server = server;
            _cancellation = context.CancellationToken.Register(() =>
            {
                _ = server.StopAsync();
                if (!Dispatcher.HasShutdownStarted)
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (ReferenceEquals(_server, server)) StopServer();
                    });
            });
            RuntimeText = $"监听中 · {BindAddress}:{Port}";
            return EditorNodeExecutionResult.Success("TCP 服务端已启动，将持续监听直至停止。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RuntimeText = "启动失败";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    public void StopServer()
    {
        VerifyAccess();
        _cancellation.Dispose();
        var server = _server;
        _server = null;
        if (server is not null) _stopping = Task.WhenAll(_stopping, server.StopAsync());
        Output.Data = null;
        Completed.Data = null;
        RuntimeText = "服务端已停止";
    }

    public Task StopServerAsync()
    {
        StopServer();
        return _stopping;
    }

    protected override void OnOwnerChanged()
    {
        base.OnOwnerChanged();
        if (Owner is null) StopServer();
    }

    public bool OpenEditor(Window? owner)
    {
        if (IsListening) StopServer();
        else
        {
            var result = Execute(new EditorExecutionContext());
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
        }
        return false;
    }
}
