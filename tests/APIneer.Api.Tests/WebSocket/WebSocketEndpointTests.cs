using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.WebSocket;

/// <summary>
/// Integration tests for the WebSocket REST management endpoints
/// (send, messages, disconnect, status).
/// Uses the API test fixture with a real test WebSocket server.
/// </summary>
public class WebSocketEndpointTests : IClassFixture<ApiTestFixture>, IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;
    private readonly TestWebSocketServer _wsServer = new();
    private HttpClient _client = null!;

    public WebSocketEndpointTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _wsServer.StartAsync();
        _client = _fixture.CreateClient();
    }

    public async Task DisposeAsync()
    {
        // Disconnect any active WS connection between tests
        await _client.DeleteAsync("/api/ws/disconnect");
        await _wsServer.DisposeAsync();
    }

    [Fact]
    public async Task Connect_ViaRest_ReturnsOpenStatus()
    {
        var url = $"{_wsServer.WsUrl}/echo";
        var response = await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(url)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("open");
        body.GetProperty("targetUrl").GetString().Should().Contain("/echo");
    }

    [Fact]
    public async Task Connect_WithoutUrl_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/ws/connect");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Connect_ToInvalidTarget_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/ws/connect?url=ws://localhost:1/nope");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("error");
        body.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Send_WhenConnected_ReturnsSentStatus()
    {
        // Connect first
        var connectUrl = $"{_wsServer.WsUrl}/echo";
        await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(connectUrl)}");

        var response = await _client.PostAsJsonAsync("/api/ws/send", new { message = "test-msg" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("sent");
        body.GetProperty("message").GetString().Should().Be("test-msg");
    }

    [Fact]
    public async Task Send_WhenNotConnected_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/ws/send", new { message = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("No active WebSocket connection");
    }

    [Fact]
    public async Task Send_EmptyMessage_ReturnsBadRequest()
    {
        var connectUrl = $"{_wsServer.WsUrl}/echo";
        await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(connectUrl)}");

        var response = await _client.PostAsJsonAsync("/api/ws/send", new { message = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Messages_AfterSend_ContainsSentAndReceived()
    {
        var connectUrl = $"{_wsServer.WsUrl}/echo";
        await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(connectUrl)}");
        await _client.PostAsJsonAsync("/api/ws/send", new { message = "hello-ws" });

        // Brief wait for echo response
        await Task.Delay(500);

        var response = await _client.GetAsync("/api/ws/messages");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messages = body.GetProperty("messages");
        messages.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // At least the sent message should be present
        var sent = messages.EnumerateArray()
            .FirstOrDefault(m => m.GetProperty("direction").GetString() == "sent");
        sent.GetProperty("content").GetString().Should().Be("hello-ws");
    }

    [Fact]
    public async Task Messages_WhenNoConnection_ReturnsStatusAndMessagesList()
    {
        // Ensure clean state
        await _client.DeleteAsync("/api/ws/disconnect");

        var response = await _client.GetAsync("/api/ws/messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("closed");
        body.TryGetProperty("messages", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnect_WhenConnected_ReturnsClosedStatus()
    {
        var connectUrl = $"{_wsServer.WsUrl}/echo";
        await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(connectUrl)}");

        var response = await _client.DeleteAsync("/api/ws/disconnect");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("closed");
    }

    [Fact]
    public async Task Disconnect_WhenNotConnected_ReturnsOk()
    {
        var response = await _client.DeleteAsync("/api/ws/disconnect");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("closed");
    }

    [Fact]
    public async Task Status_WhenConnected_ShowsOpenAndTargetUrl()
    {
        var connectUrl = $"{_wsServer.WsUrl}/echo";
        await _client.GetAsync($"/api/ws/connect?url={Uri.EscapeDataString(connectUrl)}");

        var response = await _client.GetAsync("/api/ws/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("open");
        body.GetProperty("targetUrl").GetString().Should().Contain("/echo");
    }

    [Fact]
    public async Task Status_WhenNotConnected_ShowsClosed()
    {
        // Ensure no lingering connection from other tests
        await _client.DeleteAsync("/api/ws/disconnect");

        var response = await _client.GetAsync("/api/ws/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("closed");
    }
}
