using APIneer.Api.WebSocket;
using FluentAssertions;

namespace APIneer.Api.Tests.WebSocket;

/// <summary>
/// Tests for the WebSocketProxy — connection, send/receive, disconnect, and error handling.
/// </summary>
public class WebSocketProxyTests : IAsyncLifetime
{
    private readonly TestWebSocketServer _server = new();

    public async Task InitializeAsync() => await _server.StartAsync();
    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task Connect_ToValidServer_StatusBecomesOpen()
    {
        using var proxy = new WebSocketProxy();

        await proxy.ConnectAsync($"{_server.WsUrl}/echo");

        proxy.Status.Should().Be(WebSocketConnectionStatus.Open);
        proxy.TargetUrl.Should().Contain("/echo");
        proxy.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Connect_ToInvalidUrl_ThrowsAndStatusBecomesError()
    {
        using var proxy = new WebSocketProxy();

        var act = () => proxy.ConnectAsync("ws://localhost:1/nonexistent");

        await act.Should().ThrowAsync<Exception>();
        proxy.Status.Should().Be(WebSocketConnectionStatus.Error);
        proxy.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Connect_WithEmptyUrl_ThrowsArgumentException()
    {
        using var proxy = new WebSocketProxy();

        var act = () => proxy.ConnectAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Connect_WithInvalidScheme_ThrowsArgumentException()
    {
        using var proxy = new WebSocketProxy();

        var act = () => proxy.ConnectAsync("ftp://example.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendAndReceive_EchoServer_MessagesRecorded()
    {
        using var proxy = new WebSocketProxy();
        await proxy.ConnectAsync($"{_server.WsUrl}/echo");

        await proxy.SendAsync("hello");

        // Wait briefly for the echo response
        await WaitForMessageCount(proxy, 2, timeoutMs: 3000);

        proxy.Messages.Should().HaveCount(2);
        proxy.Messages[0].Direction.Should().Be("sent");
        proxy.Messages[0].Content.Should().Be("hello");
        proxy.Messages[1].Direction.Should().Be("received");
        proxy.Messages[1].Content.Should().Be("echo: hello");

        await proxy.DisconnectAsync();
    }

    [Fact]
    public async Task Send_WhenNotConnected_ThrowsInvalidOperation()
    {
        using var proxy = new WebSocketProxy();

        var act = () => proxy.SendAsync("hello");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Disconnect_ClosesConnection()
    {
        using var proxy = new WebSocketProxy();
        await proxy.ConnectAsync($"{_server.WsUrl}/echo");
        proxy.Status.Should().Be(WebSocketConnectionStatus.Open);

        await proxy.DisconnectAsync();

        proxy.Status.Should().Be(WebSocketConnectionStatus.Closed);
    }

    [Fact]
    public async Task Disconnect_WhenNotConnected_StatusIsClosed()
    {
        using var proxy = new WebSocketProxy();

        await proxy.DisconnectAsync();

        proxy.Status.Should().Be(WebSocketConnectionStatus.Closed);
    }

    [Fact]
    public async Task Connect_WhenAlreadyConnected_ThrowsInvalidOperation()
    {
        using var proxy = new WebSocketProxy();
        await proxy.ConnectAsync($"{_server.WsUrl}/echo");

        var act = () => proxy.ConnectAsync($"{_server.WsUrl}/echo");

        await act.Should().ThrowAsync<InvalidOperationException>();

        await proxy.DisconnectAsync();
    }

    [Fact]
    public async Task MultipleMessages_AllRecordedInHistory()
    {
        using var proxy = new WebSocketProxy();
        await proxy.ConnectAsync($"{_server.WsUrl}/echo");

        await proxy.SendAsync("msg1");
        await WaitForMessageCount(proxy, 2, timeoutMs: 3000);

        await proxy.SendAsync("msg2");
        await WaitForMessageCount(proxy, 4, timeoutMs: 3000);

        proxy.Messages.Should().HaveCount(4);
        proxy.Messages.Where(m => m.Direction == "sent").Should().HaveCount(2);
        proxy.Messages.Where(m => m.Direction == "received").Should().HaveCount(2);

        await proxy.DisconnectAsync();
    }

    [Fact]
    public async Task ClearHistory_RemovesAllMessages()
    {
        using var proxy = new WebSocketProxy();
        await proxy.ConnectAsync($"{_server.WsUrl}/echo");

        await proxy.SendAsync("hello");
        await WaitForMessageCount(proxy, 2, timeoutMs: 3000);

        proxy.ClearHistory();

        proxy.Messages.Should().BeEmpty();

        await proxy.DisconnectAsync();
    }

    [Fact]
    public async Task Messages_HaveTimestamps()
    {
        using var proxy = new WebSocketProxy();
        var before = DateTime.UtcNow;

        await proxy.ConnectAsync($"{_server.WsUrl}/echo");
        await proxy.SendAsync("time-check");
        await WaitForMessageCount(proxy, 2, timeoutMs: 3000);

        var after = DateTime.UtcNow;

        foreach (var msg in proxy.Messages)
        {
            msg.Timestamp.Should().BeOnOrAfter(before);
            msg.Timestamp.Should().BeOnOrBefore(after);
        }

        await proxy.DisconnectAsync();
    }

    [Fact]
    public async Task Connect_AcceptsHttpScheme_ConvertsToWs()
    {
        using var proxy = new WebSocketProxy();

        // The server uses http, which should be converted to ws internally
        await proxy.ConnectAsync($"{_server.BaseUrl}/echo");

        proxy.Status.Should().Be(WebSocketConnectionStatus.Open);

        await proxy.DisconnectAsync();
    }

    /// <summary>
    /// Waits until the proxy has accumulated at least the expected number of messages.
    /// </summary>
    private static async Task WaitForMessageCount(WebSocketProxy proxy, int expected, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (proxy.Messages.Count < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }
}
