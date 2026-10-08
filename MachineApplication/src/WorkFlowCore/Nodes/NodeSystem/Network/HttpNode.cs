using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public abstract class HttpNode : NetworkNode
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    protected HttpNode(string title) : base(title, typeof(string))
    {
        UrlInput = AddInput("URL", () => Url);
    }

    [XTNodeProperty("URL", "仅支持 http 和 https。")]
    public string Url { get; set; } = "http://127.0.0.1:8080/";
    [XTNodeProperty("请求头（JSON）", "字符串值对象，例如 {\"Accept\":\"application/json\"}；不要把凭据写进流程文件。")]
    public string HeadersJson { get; set; } = "{}";
    public XTNodeOption UrlInput { get; }

    protected override void ValidateSettings()
    {
        base.ValidateSettings();
        using var headers = JsonDocument.Parse(HeadersJson);
        if (headers.RootElement.ValueKind != JsonValueKind.Object || headers.RootElement.EnumerateObject().Any(header => header.Value.ValueKind != JsonValueKind.String))
            throw new ArgumentException("请求头必须为字符串值的 JSON 对象。");
    }

    protected override void ValidateValues(object?[] values)
    {
        if (!Uri.TryCreate(SystemValues.Text(values[0]), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("URL 必须为绝对 HTTP/HTTPS 地址。");
    }

    protected async Task<string> RequestAsync(object?[] values, string? body, string? mediaType, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, SystemValues.Text(values[0]));
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, mediaType!);
        using var headers = JsonDocument.Parse(HeadersJson);
        foreach (var header in headers.RootElement.EnumerateObject()) request.Headers.Add(header.Name, header.Value.GetString());
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        var bytes = await ReadResponseAsync(stream, 0, timeout.Token).ConfigureAwait(false);
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        return (string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset)).GetString(bytes);
    }
}
