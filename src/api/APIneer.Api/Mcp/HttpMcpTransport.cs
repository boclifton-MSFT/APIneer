using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace APIneer.Api.Mcp;

public class HttpMcpTransport : IMcpTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, string>? _customHeaders;
    private string? _sessionId;
    private bool _disposed;

    private const string ProtocolVersion = "2025-11-25";

    public HttpMcpTransport(string url, Dictionary<string, string>? headers = null, ILogger? logger = null)
    {
        _url = url;
        _logger = logger;
        _customHeaders = headers;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _url) { Content = content };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Headers.Add("MCP-Protocol-Version", ProtocolVersion);

        if (_sessionId is not null)
            httpRequest.Headers.Add("MCP-Session-Id", _sessionId);

        if (_customHeaders is not null)
            foreach (var (key, value) in _customHeaders)
                httpRequest.Headers.TryAddWithoutValidation(key, value);

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        // Track session ID
        if (response.Headers.TryGetValues("MCP-Session-Id", out var sessionValues))
            _sessionId = sessionValues.FirstOrDefault();

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType == "text/event-stream")
            return await ParseSseResponse(response, ct);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<JsonRpcResponse>(responseJson)
            ?? throw new InvalidOperationException("Failed to parse JSON-RPC response");
    }

    public async Task SendNotificationAsync(string method, object? @params = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var notification = new JsonRpcNotification { Method = method, Params = @params };
        var json = JsonSerializer.Serialize(notification);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _url) { Content = content };
        httpRequest.Headers.Add("MCP-Protocol-Version", ProtocolVersion);

        if (_sessionId is not null)
            httpRequest.Headers.Add("MCP-Session-Id", _sessionId);

        if (_customHeaders is not null)
            foreach (var (key, value) in _customHeaders)
                httpRequest.Headers.TryAddWithoutValidation(key, value);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonRpcResponse> ParseSseResponse(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var data = line["data: ".Length..];
                if (string.IsNullOrWhiteSpace(data)) continue;

                try
                {
                    var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(data);
                    if (rpcResponse is not null)
                        return rpcResponse;
                }
                catch (JsonException)
                {
                    // Skip non-response SSE events
                }
            }
        }

        throw new InvalidOperationException("SSE stream ended without a JSON-RPC response");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Send DELETE to terminate session if we have one
        if (_sessionId is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, _url);
                request.Headers.Add("MCP-Session-Id", _sessionId);

                if (_customHeaders is not null)
                    foreach (var (key, value) in _customHeaders)
                        request.Headers.TryAddWithoutValidation(key, value);

                await _httpClient.SendAsync(request);
            }
            catch { /* best effort */ }
        }

        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
