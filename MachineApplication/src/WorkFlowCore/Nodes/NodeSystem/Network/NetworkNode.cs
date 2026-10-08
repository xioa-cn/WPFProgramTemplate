using System.IO;
using System.Net;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class NetworkNode : SystemNode
{
    protected NetworkNode(string title, Type outputType) : base(title, outputType) { }

    [XTNodeProperty("超时（毫秒）", "连接、发送及接收的总超时，必须大于 0。")]
    public int TimeoutMilliseconds { get; set; } = 5000;

    [XTNodeProperty("最大接收字节数", "接收大小上限，防止无限读取；默认 1 MiB，最多 64 MiB。")]
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    protected override void ValidateSettings()
    {
        if (TimeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMilliseconds));
        if (MaxResponseBytes is < 1 or > 64 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxResponseBytes));
    }

    protected CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeoutMilliseconds);
        return timeout;
    }

    protected static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "端口必须为 1 到 65535。");
    }

    protected static IPAddress LocalAddress(string address) => IPAddress.TryParse(address, out var parsed)
        ? parsed : throw new ArgumentException("监听地址必须为 IPv4 或 IPv6 地址。");

    protected async Task<byte[]> ReadResponseAsync(Stream stream, int expectedBytes, CancellationToken cancellationToken)
    {
        using var result = new MemoryStream();
        var buffer = new byte[System.Math.Min(MaxResponseBytes + 1, 8192)];
        while (expectedBytes == 0 || result.Length < expectedBytes)
        {
            var remaining = expectedBytes > 0 ? expectedBytes - (int)result.Length : MaxResponseBytes + 1 - (int)result.Length;
            var count = await stream.ReadAsync(buffer.AsMemory(0, System.Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                if (expectedBytes > 0 && result.Length != expectedBytes) throw new EndOfStreamException("连接在接收完整数据前关闭。");
                break;
            }
            result.Write(buffer, 0, count);
            if (result.Length > MaxResponseBytes) throw new IOException("响应超过最大接收字节数。");
        }
        return result.ToArray();
    }
}
