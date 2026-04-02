namespace APIneer.Api.Mcp;

public interface IMcpTransport : IAsyncDisposable
{
    Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken ct = default);
    Task SendNotificationAsync(string method, object? @params = null, CancellationToken ct = default);
}
