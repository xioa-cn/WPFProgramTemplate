using System.Windows;
using System.Windows.Threading;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class TcpConnectionNode : NetworkNode, IEditorCustomEditorNode
{
    private ITcpConnection? _connection;
    private TcpConnectionRegistry? _registry;
    private string? _registeredKey;
    private object? _settings;
    private CancellationTokenRegistration _cancellation;
    private Task _stopping = Task.CompletedTask;
    private int _generation;
    private bool _manuallyStopped;

    protected TcpConnectionNode(string title) : base(title, typeof(byte[]))
    {
        Output.Text = "收到数据";
        Completed.Text = "收到数据触发";
        Loaded += OnLoaded;
        Unloaded += (_, _) => StopConnection(false);
    }

    [XTNodeProperty("连接 Key", "同一画布内唯一、不区分大小写；发送节点通过此 Key 使用连接，修改后需重启。")]
    public string Key { get; set; } = "tcp1";

    [XTNodeProperty("自动启动", "页面显示后自动启动；反序列化校验不启动。手动停止后需手动启动。")]
    public bool AutoStart { get; set; }

    [XTNodeProperty("自动重连", "客户端连接失败或断线后重试；服务端监听失败后重试监听。手动停止或取消后不再重试。")]
    public bool AutoReconnect { get; set; } = true;

    [XTNodeProperty("重连间隔（毫秒）", "两次连接或监听尝试的间隔，100 到 600000 毫秒。")]
    public int ReconnectIntervalMilliseconds { get; set; } = 1000;

    [XTNodeProperty("接收长度", "0 按网络读取块输出，不保证消息边界；正数按固定字节长度输出，不等待对端关闭。")]
    public int ReceiveBytes { get; set; }

    public bool IsRunning => _connection?.IsRunning == true;
    public bool IsConnected => _connection?.IsConnected == true;
    public string EditorButtonText => "启动 / 停止 TCP 连接";
    private protected ITcpConnection? Connection => _connection;
    private protected TcpConnectionOptions Options => new(ReceiveBytes, MaxResponseBytes, TimeoutMilliseconds,
        AutoReconnect, ReconnectIntervalMilliseconds);

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        if (ReceiveBytes < 0 || ReceiveBytes > MaxResponseBytes) throw new ArgumentOutOfRangeException(nameof(ReceiveBytes));
        if (ReconnectIntervalMilliseconds is < 100 or > 600000)
            throw new ArgumentOutOfRangeException(nameof(ReconnectIntervalMilliseconds));
    }

    private protected abstract object GetSettings();
    private protected abstract ITcpConnection CreateConnection(object settings, Action<byte[], string> received,
        Action<string> reportState);

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        var generation = _generation;
        try
        {
            await _stopping;
            if (_generation == generation && IsLoaded && AutoStart && !_manuallyStopped && !IsRunning && Owner is not null)
                Execute(new EditorExecutionContext());
        }
        catch (Exception exception)
        {
            if (_generation == generation) RuntimeText = $"自动启动失败：{exception.Message}";
        }
    }

    public override EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ValidateSettings();
            var owner = Owner ?? throw new InvalidOperationException("请先将 TCP 连接节点添加到画布。");
            var settings = GetSettings();
            var key = Key.Trim();
            if (IsRunning)
            {
                if (!Equals(_settings, settings) || !StringComparer.OrdinalIgnoreCase.Equals(_registeredKey, key))
                    return EditorNodeExecutionResult.Failure("TCP 连接正在运行；修改配置后请先停止，再重新启动。");
                return EditorNodeExecutionResult.Success("TCP 连接服务已启动，不重复启动。");
            }
            StopConnection(false);
            if (!_stopping.IsCompleted) return EditorNodeExecutionResult.Failure("TCP 连接正在停止，请稍后启动。");
            var generation = _generation;
            _connection = CreateConnection(settings,
                (bytes, clientId) => Post(generation, () =>
                {
                    PublishClientId(clientId);
                    Output.TransferData(bytes);
                    Completed.TransferData(new EditorFlowSignal(context.ExecutionId));
                    RuntimeText = $"{key} · 收到 {bytes.Length} 字节";
                }),
                state => Post(generation, () => RuntimeText = $"{key} · {state}"));
            _registry = TcpConnectionRegistry.For(owner);
            _registeredKey = key;
            _registry.Register(key, _connection);
            _settings = settings;
            _manuallyStopped = false;
            RuntimeText = $"{key} · 正在启动…";
            _connection.Start();
            var connection = _connection;
            _cancellation = context.CancellationToken.Register(() =>
            {
                _ = connection.StopAsync();
                Post(generation, () => StopConnection(true));
            });
            return EditorNodeExecutionResult.Success("TCP 连接服务已启动，连接状态见节点；接收与发送独立运行。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StopConnection(false);
            RuntimeText = $"启动失败：{exception.Message}";
            return EditorNodeExecutionResult.Failure(exception.Message);
        }
    }

    protected virtual void PublishClientId(string clientId) { }

    private void Post(int generation, Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (_generation == generation && _connection is not null) action();
        }, DispatcherPriority.Background);
    }

    private void StopConnection(bool manual)
    {
        VerifyAccess();
        if (manual) _manuallyStopped = true;
        _generation++;
        _cancellation.Dispose();
        var connection = _connection;
        _connection = null;
        if (connection is not null)
        {
            if (_registeredKey is not null) _registry?.Remove(_registeredKey, connection);
            _stopping = Task.WhenAll(_stopping, connection.StopAsync());
        }
        _registry = null;
        _registeredKey = null;
        _settings = null;
        foreach (var output in GetOutputOptions()) output.Data = null;
        RuntimeText = "TCP 连接已停止";
    }

    public void Stop() => StopConnection(true);

    public Task StopAsync()
    {
        Stop();
        return _stopping;
    }

    protected override void OnOwnerChanged()
    {
        base.OnOwnerChanged();
        if (Owner is null) StopConnection(false);
    }

    public bool OpenEditor(Window? owner)
    {
        if (IsRunning) Stop();
        else
        {
            var result = Execute(new EditorExecutionContext());
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
        }
        return false;
    }
}
