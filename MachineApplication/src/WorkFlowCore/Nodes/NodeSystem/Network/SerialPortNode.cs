using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("串口收发")]
[XTNode("串口通信", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "执行时打开串口，发送后读取指定字节数，结束即关闭；接收长度 0 表示只发送。")]
public sealed class SerialPortNode : SocketNode
{
    public SerialPortNode() : base("串口收发") { }
    [XTNodeProperty("串口名称", "例如 COM1。")]
    public string PortName { get; set; } = "COM1";
    [XTNodeProperty("波特率", "必须为正数。")]
    public int BaudRate { get; set; } = 9600;
    [XTNodeProperty("数据位", "5 到 8。")]
    public int DataBits { get; set; } = 8;
    [XTNodeProperty("校验位", "None、Odd、Even、Mark、Space。")]
    public Parity Parity { get; set; } = Parity.None;
    [XTNodeProperty("停止位", "One、Two 或 OnePointFive。")]
    public StopBits StopBits { get; set; } = StopBits.One;
    [XTNodeProperty("握手方式", "None、XOnXOff、RequestToSend 或 RequestToSendXOnXOff。")]
    public Handshake Handshake { get; set; } = Handshake.None;

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        ArgumentException.ThrowIfNullOrWhiteSpace(PortName);
        if (BaudRate <= 0 || DataBits is < 5 or > 8 || !Enum.IsDefined(Parity) || !Enum.IsDefined(StopBits) ||
            StopBits == StopBits.None || !Enum.IsDefined(Handshake)) throw new ArgumentException("串口设置无效。");
    }

    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        using var serial = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
        {
            Handshake = Handshake,
            ReadTimeout = System.Math.Min(100, TimeoutMilliseconds),
            WriteTimeout = TimeoutMilliseconds
        };
        serial.Open();
        var payload = Payload(values[0]);
        void CheckDeadline()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (elapsed.ElapsedMilliseconds >= TimeoutMilliseconds) throw new TimeoutException("串口操作超时。");
        }
        for (var offset = 0; offset < payload.Length; offset += 256)
        {
            CheckDeadline();
            serial.WriteTimeout = (int)System.Math.Max(1, TimeoutMilliseconds - elapsed.ElapsedMilliseconds);
            serial.Write(payload, offset, System.Math.Min(256, payload.Length - offset));
        }
        var result = new byte[ReceiveBytes];
        var received = 0;
        while (received < result.Length)
        {
            CheckDeadline();
            try { received += serial.Read(result, received, result.Length - received); }
            catch (TimeoutException) { CheckDeadline(); }
        }
        CheckDeadline();
        return result;
    }
}
