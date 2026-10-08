using System.Windows;
using System.Windows.Media;

namespace ST.Library.UI.NodeEditor;

/// <summary>节点端口，统一校验方向、类型、单连接限制及环路；连接只能存在于同一编辑器。</summary>
public class XTNodeOption
{
    public static readonly XTNodeOption Empty = new();
    protected readonly HashSet<XTNodeOption> m_hs_connected = [];
    private object? _data;
    private bool _hasDefaultValue;
    private XTNodeOption() { Text = ""; }
    public XTNodeOption(string text, Type dataType, bool single)
    {
        Text = text;
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        IsSingle = single;
    }
    public XTNode? Owner { get; internal set; }
    public bool IsSingle { get; }
    public bool IsInput { get; internal set; }
    public string Text { get; internal set; }
    public string Description { get; set; } = string.Empty;
    public Type? DataType { get; internal set; }
    public Color TextColor { get; internal set; } = Colors.Transparent;
    public Color DotColor { get; internal set; } = Colors.Transparent;
    public double DotLeft { get; internal set; }
    public double DotTop { get; internal set; }
    public double DotSize { get; protected set; } = 10;
    public Rect TextRectangle { get; internal set; }
    public Rect DotRectangle => new(DotLeft, DotTop, DotSize, DotSize);
    public Point Center => new(DotLeft + DotSize / 2, DotTop + DotSize / 2);
    public int ConnectionCount => m_hs_connected.Count;
    public bool HasDefaultValue
    {
        get => _hasDefaultValue;
        set
        {
            if (_hasDefaultValue == value) return;
            _hasDefaultValue = value;
            Invalidate();
        }
    }
    public bool IsUsingDefaultValue => IsInput && HasDefaultValue && ConnectionCount == 0 && Data is null;
    public IReadOnlyCollection<XTNodeOption> ConnectedOption => m_hs_connected.ToArray();
    public object? Data
    {
        get => _data;
        set
        {
            if (value is not null && (DataType is null || !DataType.IsInstanceOfType(value)))
                throw new ArgumentException("数据类型与端口不兼容。", nameof(value));
            _data = value;
            if (HasDefaultValue) Invalidate();
        }
    }
    public event XTNodeOptionEventHandler? Connecting;
    public event XTNodeOptionEventHandler? Connected;
    public event XTNodeOptionEventHandler? DisConnecting;
    public event XTNodeOptionEventHandler? DisConnected;
    public event XTNodeOptionEventHandler? DataTransfer;

    /// <summary>更新端口数据类型；已连接端口必须先断开，避免已有连接变成非法状态。</summary>
    public void SetDataType(Type dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        Owner?.Owner?.VerifyAccess();
        if (DataType == dataType) return;
        if (ConnectionCount > 0)
            throw new InvalidOperationException("请先断开端口连接，再修改数据类型。");
        DataType = dataType;
        _data = null;
        Invalidate();
    }
    protected internal virtual void OnConnecting(XTNodeOptionEventArgs args) => Connecting?.Invoke(this, args);
    protected internal virtual void OnConnected(XTNodeOptionEventArgs args) => Connected?.Invoke(this, args);
    protected internal virtual void OnDisConnecting(XTNodeOptionEventArgs args) => DisConnecting?.Invoke(this, args);
    protected internal virtual void OnDisConnected(XTNodeOptionEventArgs args) => DisConnected?.Invoke(this, args);
    protected internal virtual void OnDataTransfer(XTNodeOptionEventArgs args) => DataTransfer?.Invoke(this, args);
    protected void Invalidate() => Owner?.Invalidate();
    internal virtual Type? ConnectionType(Type? otherType) => DataType;

    /// <summary>只查询连接合法性，不修改连接状态；两端单连接限制都会检查。</summary>
    public virtual ConnectionStatus CanConnect(XTNodeOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (this == Empty || option == Empty) return ConnectionStatus.EmptyOption;
        if (Owner?.Owner is null || option.Owner?.Owner != Owner.Owner) return ConnectionStatus.NoOwner;
        if (Owner == option.Owner) return ConnectionStatus.SameOwner;
        if (IsInput == option.IsInput) return ConnectionStatus.SameInputOrOutput;
        if (Owner.LockOption || option.Owner.LockOption) return ConnectionStatus.Locked;
        if (m_hs_connected.Contains(option)) return ConnectionStatus.Exists;
        if ((IsSingle && ConnectionCount > 0) || (option.IsSingle && option.ConnectionCount > 0)) return ConnectionStatus.SingleOption;
        var input = IsInput ? this : option;
        var output = IsInput ? option : this;
        var inputType = input.ConnectionType(output.DataType);
        var outputType = output.ConnectionType(input.DataType);
        if (inputType is null || outputType is null || !inputType.IsAssignableFrom(outputType))
            return ConnectionStatus.ErrorType;
        return XTNodeEditor.CanFindNodePath(input.Owner!, output.Owner!) ? ConnectionStatus.Loop : ConnectionStatus.Connected;
    }

    /// <summary>双方和编辑器均同意后，先提交双向关系，再发布连接事件。</summary>
    public virtual ConnectionStatus ConnectOption(XTNodeOption option)
    {
        Owner?.VerifyAccess();
        var status = CanConnect(option);
        if (status != ConnectionStatus.Connected) return status;
        status = option.CanConnect(this);
        if (status != ConnectionStatus.Connected) return status;
        var editor = Owner!.Owner!;
        var args = new XTNodeEditorOptionEventArgs(option, this, ConnectionStatus.Connecting);
        editor.OnOptionConnecting(args);
        if (!args.Continue || !ConnectingOption(option) || !option.ConnectingOption(this)) return ConnectionStatus.Reject;
        status = CanConnect(option);
        if (status != ConnectionStatus.Connected) return status;
        status = option.CanConnect(this);
        if (status != ConnectionStatus.Connected) return status;
        m_hs_connected.Add(option);
        option.m_hs_connected.Add(this);
        editor.AddConnection(IsInput ? option : this, IsInput ? this : option);
        OnConnected(new(true, option, ConnectionStatus.Connected));
        option.OnConnected(new(false, this, ConnectionStatus.Connected));
        var input = IsInput ? this : option;
        input.OnDataTransfer(new(false, IsInput ? option : this, ConnectionStatus.Connected));
        editor.OnOptionConnected(new(option, this, ConnectionStatus.Connected));
        return ConnectionStatus.Connected;
    }

    protected virtual bool ConnectingOption(XTNodeOption option)
    {
        var args = new XTNodeOptionEventArgs(true, option, ConnectionStatus.Connecting);
        OnConnecting(args);
        return args.Status != ConnectionStatus.Reject;
    }
    protected virtual bool DisConnectingOption(XTNodeOption option)
    {
        var args = new XTNodeOptionEventArgs(true, option, ConnectionStatus.DisConnecting);
        OnDisConnecting(args);
        return args.Status != ConnectionStatus.Reject;
    }

    public virtual ConnectionStatus DisConnectOption(XTNodeOption option)
    {
        Owner?.VerifyAccess();
        if (!m_hs_connected.Contains(option)) return ConnectionStatus.DisConnected;
        if (Owner?.LockOption == true || option.Owner?.LockOption == true) return ConnectionStatus.Locked;
        var args = new XTNodeEditorOptionEventArgs(option, this, ConnectionStatus.DisConnecting);
        Owner?.Owner?.OnOptionDisConnecting(args);
        if (!args.Continue || !DisConnectingOption(option) || !option.DisConnectingOption(this)) return ConnectionStatus.Reject;
        DisconnectCore(option);
        return ConnectionStatus.DisConnected;
    }

    /// <summary>移除节点时强制清理双向引用，锁定状态不能留下悬空连接。</summary>
    internal void DisconnectCore(XTNodeOption option)
    {
        if (!m_hs_connected.Remove(option)) return;
        option.m_hs_connected.Remove(this);
        var editor = Owner?.Owner;
        editor?.RemoveConnection(this, option);
        OnDisConnected(new(true, option, ConnectionStatus.DisConnected));
        option.OnDisConnected(new(false, this, ConnectionStatus.DisConnected));
        var input = IsInput ? this : option;
        input.OnDataTransfer(new(false, IsInput ? option : this, ConnectionStatus.DisConnected));
        editor?.OnOptionDisConnected(new(option, this, ConnectionStatus.DisConnected));
    }
    public void DisConnectionAll()
    {
        foreach (var option in m_hs_connected.ToArray()) DisConnectOption(option);
    }
    internal void Detach()
    {
        foreach (var option in m_hs_connected.ToArray()) DisconnectCore(option);
    }
    public List<XTNodeOption> GetConnectedOption() => Owner?.Owner?.GetConnectionInfo()
        .Where(connection => connection.Input == this || connection.Output == this)
        .Select(connection => IsInput ? connection.Output : connection.Input).ToList() ?? [];

    /// <summary>按当前连接快照广播，允许回调内修改连接；接收者通过 TargetOption.Data 读取数据。</summary>
    public void TransferData()
    {
        foreach (var option in m_hs_connected.ToArray())
            if (m_hs_connected.Contains(option)) option.OnDataTransfer(new(true, this, ConnectionStatus.Connected));
    }
    public void TransferData(object? data) { Data = data; TransferData(); }
    public void TransferData(object? data, bool disposeOld)
    {
        var previous = Data;
        Data = data;
        if (disposeOld && !ReferenceEquals(previous, data) && previous is IDisposable disposable) disposable.Dispose();
        TransferData();
    }
}
