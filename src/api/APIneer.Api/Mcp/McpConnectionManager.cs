using System.Collections.Concurrent;

namespace APIneer.Api.Mcp;

public class McpConnectionManager(ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, McpConnection> _connections = new();

    public McpConnection CreateConnection()
    {
        var logger = loggerFactory.CreateLogger<McpConnection>();
        var connection = new McpConnection(logger);
        _connections[connection.Id] = connection;
        return connection;
    }

    public McpConnection? GetConnection(Guid id) =>
        _connections.TryGetValue(id, out var conn) ? conn : null;

    public IReadOnlyList<McpConnection> GetAllConnections() =>
        _connections.Values.ToList();

    public async Task<bool> RemoveConnectionAsync(Guid id)
    {
        if (!_connections.TryRemove(id, out var connection))
            return false;

        await connection.DisposeAsync();
        return true;
    }

    public IMcpTransport CreateTransport(string transportType, string? command, string[]? args,
        Dictionary<string, string>? envVars, string? url, Dictionary<string, string>? headers = null)
    {
        var logger = loggerFactory.CreateLogger(
            transportType == "stdio" ? typeof(StdioMcpTransport) : typeof(HttpMcpTransport));

        return transportType switch
        {
            "stdio" => new StdioMcpTransport(
                command ?? throw new ArgumentException("Command is required for stdio transport"),
                args, envVars, logger),
            "streamable-http" => new HttpMcpTransport(
                url ?? throw new ArgumentException("URL is required for streamable-http transport"),
                headers, logger),
            _ => throw new ArgumentException($"Unknown transport type: {transportType}")
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
            await connection.DisposeAsync();
        _connections.Clear();
        GC.SuppressFinalize(this);
    }
}
