using System.Text.Json;

namespace APIneer.Api.Mcp;

public enum McpConnectionState
{
    Disconnected,
    Connecting,
    Connected
}

public class McpConnection(ILogger? logger = null) : IAsyncDisposable
{
    private IMcpTransport? _transport;
    private int _nextId;

    public Guid Id { get; } = Guid.NewGuid();
    public McpConnectionState State { get; private set; } = McpConnectionState.Disconnected;
    public JsonElement? ServerCapabilities { get; private set; }
    public JsonElement? ServerInfo { get; private set; }
    public Guid? ServerConfigId { get; set; }

    public async Task ConnectAsync(IMcpTransport transport, CancellationToken ct = default)
    {
        _transport = transport;
        State = McpConnectionState.Connecting;

        try
        {
            var initRequest = new JsonRpcRequest
            {
                Id = NextId(),
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "APIneer",
                        version = "1.0.0"
                    }
                }
            };

            var response = await _transport.SendRequestAsync(initRequest, ct);

            if (response.Error is not null)
            {
                State = McpConnectionState.Disconnected;
                throw new InvalidOperationException(
                    $"MCP initialize failed: [{response.Error.Code}] {response.Error.Message}");
            }

            if (response.Result.HasValue)
            {
                var result = response.Result.Value;
                if (result.TryGetProperty("capabilities", out var caps))
                    ServerCapabilities = caps.Clone();
                if (result.TryGetProperty("serverInfo", out var info))
                    ServerInfo = info.Clone();
            }

            await _transport.SendNotificationAsync("notifications/initialized", ct: ct);

            State = McpConnectionState.Connected;
            logger?.LogInformation("MCP connection {Id} established", Id);
        }
        catch
        {
            State = McpConnectionState.Disconnected;
            throw;
        }
    }

    public Task<JsonRpcResponse> ListToolsAsync(CancellationToken ct = default) =>
        SendAsync("tools/list", null, ct);

    public Task<JsonRpcResponse> CallToolAsync(string name, object? arguments = null, CancellationToken ct = default) =>
        SendAsync("tools/call", new { name, arguments }, ct);

    public Task<JsonRpcResponse> ListResourcesAsync(CancellationToken ct = default) =>
        SendAsync("resources/list", null, ct);

    public Task<JsonRpcResponse> ReadResourceAsync(string uri, CancellationToken ct = default) =>
        SendAsync("resources/read", new { uri }, ct);

    public Task<JsonRpcResponse> ListPromptsAsync(CancellationToken ct = default) =>
        SendAsync("prompts/list", null, ct);

    public Task<JsonRpcResponse> GetPromptAsync(string name, object? arguments = null, CancellationToken ct = default) =>
        SendAsync("prompts/get", new { name, arguments }, ct);

    public Task<JsonRpcResponse> PingAsync(CancellationToken ct = default) =>
        SendAsync("ping", null, ct);

    public async Task DisconnectAsync()
    {
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
        State = McpConnectionState.Disconnected;
        logger?.LogInformation("MCP connection {Id} disconnected", Id);
    }

    private async Task<JsonRpcResponse> SendAsync(string method, object? @params, CancellationToken ct)
    {
        if (State != McpConnectionState.Connected || _transport is null)
            throw new InvalidOperationException("Not connected to MCP server");

        var request = new JsonRpcRequest
        {
            Id = NextId(),
            Method = method,
            Params = @params
        };

        return await _transport.SendRequestAsync(request, ct);
    }

    private int NextId() => Interlocked.Increment(ref _nextId);

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
