using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows.Media.Imaging;

namespace AIAgentFrameworkModule.Services;

internal static class ChatImageLoader
{
    internal const int MaximumBytes = 8 * 1024 * 1024;
    private static readonly SemaphoreSlim DownloadSlots = new(3);
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectCallback = ConnectAsync,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });

    internal static async Task<BitmapSource> LoadAsync(string source, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await DownloadSlots.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                byte[] bytes = source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    ? ReadDataImage(source)
                    : await DownloadAsync(source, timeout.Token).ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
                using MemoryStream stream = new(bytes, false);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 ||
                    (long)frame.PixelWidth * frame.PixelHeight > 40_000_000)
                    throw new InvalidDataException("图片分辨率过大。");
                stream.Position = 0;
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                if (frame.PixelWidth >= frame.PixelHeight) bitmap.DecodePixelWidth = Math.Min(frame.PixelWidth, 1280);
                else bitmap.DecodePixelHeight = Math.Min(frame.PixelHeight, 1280);
                bitmap.EndInit();
                bitmap.Freeze();
                timeout.Token.ThrowIfCancellationRequested();
                return (BitmapSource)bitmap;
            }, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            DownloadSlots.Release();
        }
    }

    private static byte[] ReadDataImage(string source)
    {
        int separator = source.IndexOf(',');
        if (separator < 0 || separator > 64 || source.Length - separator - 1 > (MaximumBytes + 2L) / 3 * 4)
            throw new InvalidDataException("图片数据无效或超过 8 MB。");
        string header = source[..separator].ToLowerInvariant();
        if (header is not ("data:image/png;base64" or "data:image/jpeg;base64" or
            "data:image/gif;base64" or "data:image/bmp;base64"))
            throw new InvalidDataException("不支持此内嵌图片格式。");
        byte[] bytes = Convert.FromBase64String(source[(separator + 1)..]);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("图片超过 8 MB。");
        return bytes;
    }

    internal static async Task<byte[]> DownloadAsync(string source, CancellationToken cancellationToken, HttpClient? client = null)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? address))
            throw new InvalidDataException("图片需要完整的 HTTP/HTTPS 地址。");
        for (int redirect = 0; redirect <= 3; redirect++)
        {
            if (address.Scheme is not ("http" or "https") || address.UserInfo.Length > 0 ||
                address.IsLoopback || (IPAddress.TryParse(address.DnsSafeHost, out IPAddress? literal) && !IsPublicAddress(literal)))
                throw new InvalidDataException("不允许加载本地文件或内网图片。");
            using HttpResponseMessage response = await (client ?? Client).GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                Uri? location = response.Headers.Location;
                if (location is null) throw new InvalidDataException("图片重定向地址无效。");
                address = new Uri(address, location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes)
                throw new InvalidDataException("图片超过 8 MB。");
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("链接返回的不是图片。");
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using MemoryStream output = new();
            byte[] buffer = new byte[16384];
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (output.Length + count > MaximumBytes) throw new InvalidDataException("图片超过 8 MB。");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        throw new InvalidDataException("图片重定向次数过多。");
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("不允许连接内网图片地址。");
        foreach (IPAddress address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("无法连接图片服务器。");
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xe0) == 0x20 &&
                !(bytes[0] == 0x20 && bytes[1] == 0x01 && (bytes[2] < 2 || (bytes[2] == 0x0d && bytes[3] == 0xb8))) &&
                !(bytes[0] == 0x20 && bytes[1] == 0x02);
        return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
            !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
            !(bytes[0] == 169 && bytes[1] == 254) &&
            !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0 || (bytes[1] == 88 && bytes[2] == 99))) &&
            !(bytes[0] == 198 && (bytes[1] is 18 or 19 || (bytes[1] == 51 && bytes[2] == 100))) &&
            !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }
}
