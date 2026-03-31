using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace APIneer.Api.WebSocket;

/// <summary>
/// Tracks the current state of a WebSocket proxy connection.
/// </summary>
public enum WebSocketConnectionStatus
{
    Connecting,
    Open,
    Closed,
    Error
}

/// <summary>
/// Represents a single message sent or received through the WebSocket proxy.
/// </summary>
public record WebSocketMessage(
    string Direction,   // "sent" or "received"
    string Content,
    DateTime Timestamp);

/// <summary>
/// DTOs for WebSocket REST endpoints.
/// </summary>
public record SendMessageDto(string Message);

/// <summary>
/// Manages a proxied WebSocket connection to a target server.
/// Relays messages bidirectionally between the API client and the target WebSocket.
/// </summary>
public sealed class WebSocketProxy : IDisposable
{
    private ClientWebSocket? _clientSocket;
    private readonly ConcurrentQueue<WebSocketMessage> _messageHistory = new();
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    /// <summary>Current connection status.</summary>
    public WebSocketConnectionStatus Status { get; private set; } = WebSocketConnectionStatus.Closed;

    /// <summary>Error message if the connection failed.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>The target URL this proxy is connected to.</summary>
    public string? TargetUrl { get; private set; }

    /// <summary>Returns the full message history (sent and received).</summary>
    public IReadOnlyList<WebSocketMessage> Messages => _messageHistory.ToArray();

    /// <summary>
    /// Connects to the target WebSocket server and begins relaying received messages.
    /// </summary>
    public async Task ConnectAsync(string targetUrl, CancellationToken cancellationToken = default)
    {
        if (Status == WebSocketConnectionStatus.Open)
            throw new InvalidOperationException("Already connected. Disconnect first.");

        if (string.IsNullOrWhiteSpace(targetUrl))
            throw new ArgumentException("Target URL is required.", nameof(targetUrl));

        // Validate and normalize the URL (accept ws://, wss://, http://, https://)
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {targetUrl}", nameof(targetUrl));

        var wsUri = uri.Scheme switch
        {
            "ws" or "wss" => uri,
            "http" => new Uri($"ws://{uri.Host}:{uri.Port}{uri.PathAndQuery}"),
            "https" => new Uri($"wss://{uri.Host}:{uri.Port}{uri.PathAndQuery}"),
            _ => throw new ArgumentException($"Unsupported scheme: {uri.Scheme}", nameof(targetUrl))
        };

        Status = WebSocketConnectionStatus.Connecting;
        TargetUrl = targetUrl;
        ErrorMessage = null;

        _clientSocket = new ClientWebSocket();
        _receiveCts = new CancellationTokenSource();

        try
        {
            await _clientSocket.ConnectAsync(wsUri, cancellationToken);
            Status = WebSocketConnectionStatus.Open;

            // Start background receive loop
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            Status = WebSocketConnectionStatus.Error;
            ErrorMessage = ex.Message;
            _clientSocket.Dispose();
            _clientSocket = null;
            throw;
        }
    }

    /// <summary>
    /// Sends a text message to the connected target WebSocket.
    /// </summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_clientSocket is null || Status != WebSocketConnectionStatus.Open)
            throw new InvalidOperationException("Not connected to a WebSocket server.");

        var bytes = Encoding.UTF8.GetBytes(message);
        await _clientSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

        _messageHistory.Enqueue(new WebSocketMessage("sent", message, DateTime.UtcNow));
    }

    /// <summary>
    /// Disconnects from the target WebSocket server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_clientSocket is null)
        {
            Status = WebSocketConnectionStatus.Closed;
            return;
        }

        try
        {
            _receiveCts?.Cancel();

            if (_clientSocket.State == WebSocketState.Open ||
                _clientSocket.State == WebSocketState.CloseReceived)
            {
                await _clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnecting",
                    cancellationToken);
            }

            // Wait for receive loop to finish
            if (_receiveTask is not null)
            {
                try { await _receiveTask; }
                catch (OperationCanceledException) { }
            }
        }
        catch (WebSocketException) { /* Already closed */ }
        finally
        {
            Status = WebSocketConnectionStatus.Closed;
            _clientSocket.Dispose();
            _clientSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
        }
    }

    /// <summary>
    /// Clears the message history.
    /// </summary>
    public void ClearHistory()
    {
        while (_messageHistory.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Background loop that continuously reads messages from the target WebSocket.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   _clientSocket?.State == WebSocketState.Open)
            {
                var result = await _clientSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Status = WebSocketConnectionStatus.Closed;
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle messages that may span multiple frames
                    var sb = new StringBuilder();
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    while (!result.EndOfMessage)
                    {
                        result = await _clientSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), cancellationToken);
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }

                    var content = sb.ToString();
                    _messageHistory.Enqueue(new WebSocketMessage("received", content, DateTime.UtcNow));
                }
            }
        }
        catch (OperationCanceledException) { /* Expected on disconnect */ }
        catch (WebSocketException)
        {
            Status = WebSocketConnectionStatus.Error;
            ErrorMessage = "Connection lost";
        }
    }

    /// <summary>
    /// Handles an incoming WebSocket upgrade request, relaying messages between
    /// the browser client and the target WebSocket server.
    /// </summary>
    public static async Task HandleUpgradeAsync(
        HttpContext context,
        string targetUrl,
        WebSocketProxy proxy)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("WebSocket upgrade required");
            return;
        }

        // Connect to the target first
        try
        {
            await proxy.ConnectAsync(targetUrl, context.RequestAborted);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 502;
            await context.Response.WriteAsync($"Failed to connect to target: {ex.Message}");
            return;
        }

        // Accept the client WebSocket
        using var clientWs = await context.WebSockets.AcceptWebSocketAsync();

        // Relay in both directions concurrently
        var clientToTarget = RelayAsync(clientWs, proxy, "sent", context.RequestAborted);
        var targetToClient = RelayFromTargetAsync(proxy, clientWs, context.RequestAborted);

        await Task.WhenAny(clientToTarget, targetToClient);

        await proxy.DisconnectAsync(CancellationToken.None);
    }

    private static async Task RelayAsync(
        System.Net.WebSockets.WebSocket source,
        WebSocketProxy proxy,
        string direction,
        CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (source.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await proxy.SendAsync(message, ct);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
    }

    private static async Task RelayFromTargetAsync(
        WebSocketProxy proxy,
        System.Net.WebSockets.WebSocket destination,
        CancellationToken ct)
    {
        // This monitors the proxy's message history for new received messages
        int lastIndex = proxy.Messages.Count;
        try
        {
            while (destination.State == WebSocketState.Open &&
                   proxy.Status == WebSocketConnectionStatus.Open &&
                   !ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct); // Poll interval
                var messages = proxy.Messages;
                for (int i = lastIndex; i < messages.Count; i++)
                {
                    var msg = messages[i];
                    if (msg.Direction == "received")
                    {
                        var bytes = Encoding.UTF8.GetBytes(msg.Content);
                        await destination.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            endOfMessage: true,
                            ct);
                    }
                }
                lastIndex = messages.Count;
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _clientSocket?.Dispose();
        _receiveCts?.Dispose();
    }
}
