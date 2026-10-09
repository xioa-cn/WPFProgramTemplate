using System.Net.Sockets;

namespace WorkFlowCore.Nodes.NodeSystem;

internal sealed class TcpPeer(TcpClient client) : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.GetStream().WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        finally { _sendLock.Release(); }
    }

    public async Task ReceiveAsync(TcpConnectionOptions options, Action<byte[]> received, CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        while (!cancellationToken.IsCancellationRequested)
        {
            var data = new byte[options.ReceiveBytes > 0 ? options.ReceiveBytes : System.Math.Min(8192, options.MaxResponseBytes)];
            var count = await stream.ReadAsync(data.AsMemory(0, options.ReceiveBytes > 0 ? 1 : data.Length), cancellationToken)
                .ConfigureAwait(false);
            if (count == 0) return;
            if (options.ReceiveBytes > 0)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(options.TimeoutMilliseconds);
                await stream.ReadExactlyAsync(data.AsMemory(1), timeout.Token).ConfigureAwait(false);
            }
            else if (count != data.Length) Array.Resize(ref data, count);
            received(data);
        }
    }

    public void Dispose() => client.Dispose();
}
