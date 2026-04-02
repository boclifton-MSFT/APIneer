using System.Text.Json;
using APIneer.Api.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace APIneer.Api.Tests.Mcp;

/// <summary>
/// Tests for McpConnection lifecycle: state transitions, protocol handshake,
/// capability extraction, send dispatch, and error handling.
/// </summary>
public class McpConnectionTests
{
    // ── Helpers ──────────────────────────────────────────────────

    private static IMcpTransport BuildSuccessTransport(
        string responseJson = """{"capabilities":{"tools":{}},"serverInfo":{"name":"TestServer","version":"1.0"}}""")
    {
        var transport = Substitute.For<IMcpTransport>();
        var doc = JsonDocument.Parse(responseJson);

        transport.SendRequestAsync(Arg.Any<JsonRpcRequest>(), Arg.Any<CancellationToken>())
            .Returns(new JsonRpcResponse { Jsonrpc = "2.0", Id = 1, Result = doc.RootElement });
        transport.SendNotificationAsync(
                Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        transport.DisposeAsync().Returns(ValueTask.CompletedTask);

        return transport;
    }

    private static IMcpTransport BuildErrorTransport(int code = -32600, string message = "Initialize failed")
    {
        var transport = Substitute.For<IMcpTransport>();

        transport.SendRequestAsync(Arg.Any<JsonRpcRequest>(), Arg.Any<CancellationToken>())
            .Returns(new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Id = 1,
                Error = new JsonRpcError { Code = code, Message = message }
            });
        transport.DisposeAsync().Returns(ValueTask.CompletedTask);

        return transport;
    }

    // ── Initial State ────────────────────────────────────────────

    [Fact]
    public void InitialState_IsDisconnected()
    {
        var conn = new McpConnection(NullLogger.Instance);
        conn.State.Should().Be(McpConnectionState.Disconnected);
    }

    [Fact]
    public void InitialState_HasNullCapabilitiesAndServerInfo()
    {
        var conn = new McpConnection();
        conn.ServerCapabilities.Should().BeNull();
        conn.ServerInfo.Should().BeNull();
    }

    [Fact]
    public void InitialState_HasUniqueId()
    {
        var a = new McpConnection();
        var b = new McpConnection();
        a.Id.Should().NotBe(b.Id);
        a.Id.Should().NotBe(Guid.Empty);
    }

    // ── ConnectAsync — happy path ────────────────────────────────

    [Fact]
    public async Task ConnectAsync_TransitionsToConnected_OnSuccess()
    {
        var conn = new McpConnection();
        await conn.ConnectAsync(BuildSuccessTransport());
        conn.State.Should().Be(McpConnectionState.Connected);
    }

    [Fact]
    public async Task ConnectAsync_PopulatesServerCapabilities()
    {
        var conn = new McpConnection();
        await conn.ConnectAsync(
            BuildSuccessTransport("""{"capabilities":{"tools":{}},"serverInfo":{"name":"T"}}"""));

        conn.ServerCapabilities.Should().NotBeNull();
        conn.ServerCapabilities!.Value.TryGetProperty("tools", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_PopulatesServerInfo()
    {
        var conn = new McpConnection();
        await conn.ConnectAsync(
            BuildSuccessTransport("""{"capabilities":{},"serverInfo":{"name":"MyServer","version":"2.0"}}"""));

        conn.ServerInfo.Should().NotBeNull();
        conn.ServerInfo!.Value.GetProperty("name").GetString().Should().Be("MyServer");
    }

    [Fact]
    public async Task ConnectAsync_SendsInitializedNotification()
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);

        await transport.Received(1).SendNotificationAsync(
            "notifications/initialized",
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_SendsInitializeRequest_WithCorrectMethod()
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);

        await transport.Received(1).SendRequestAsync(
            Arg.Is<JsonRpcRequest>(r => r.Method == "initialize"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_ResponseWithoutCapabilities_DoesNotThrow()
    {
        var conn = new McpConnection();
        // Response has a result but no "capabilities" / "serverInfo" keys
        var transport = BuildSuccessTransport("""{}""");
        await conn.ConnectAsync(transport);

        conn.State.Should().Be(McpConnectionState.Connected);
        conn.ServerCapabilities.Should().BeNull();
        conn.ServerInfo.Should().BeNull();
    }

    // ── ConnectAsync — error path ────────────────────────────────

    [Fact]
    public async Task ConnectAsync_ThrowsInvalidOperationException_WhenServerReturnsError()
    {
        var conn = new McpConnection();
        var act = () => conn.ConnectAsync(BuildErrorTransport(-32001, "Protocol mismatch"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MCP initialize failed*");
    }

    [Fact]
    public async Task ConnectAsync_RemainsDisconnected_WhenServerReturnsError()
    {
        var conn = new McpConnection();
        try { await conn.ConnectAsync(BuildErrorTransport()); } catch { /* expected */ }
        conn.State.Should().Be(McpConnectionState.Disconnected);
    }

    [Fact]
    public async Task ConnectAsync_ErrorMessage_IncludesServerCodeAndMessage()
    {
        var conn = new McpConnection();
        var act = () => conn.ConnectAsync(BuildErrorTransport(-32099, "Custom server error"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*-32099*Custom server error*");
    }

    // ── Send methods — not-connected guard ──────────────────────

    [Theory]
    [InlineData("ListTools")]
    [InlineData("CallTool")]
    [InlineData("ListResources")]
    [InlineData("ReadResource")]
    [InlineData("ListPrompts")]
    [InlineData("GetPrompt")]
    [InlineData("Ping")]
    public async Task SendMethods_ThrowInvalidOperationException_WhenNotConnected(string method)
    {
        var conn = new McpConnection();

        Func<Task> act = method switch
        {
            "ListTools"     => () => conn.ListToolsAsync(),
            "CallTool"      => () => conn.CallToolAsync("some-tool"),
            "ListResources" => () => conn.ListResourcesAsync(),
            "ReadResource"  => () => conn.ReadResourceAsync("resource://test"),
            "ListPrompts"   => () => conn.ListPromptsAsync(),
            "GetPrompt"     => () => conn.GetPromptAsync("my-prompt"),
            "Ping"          => () => conn.PingAsync(),
            _               => throw new InvalidOperationException("Unknown method in test")
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not connected*");
    }

    // ── Send methods — dispatches correct JSON-RPC method ───────

    [Theory]
    [InlineData("tools/list")]
    [InlineData("tools/call")]
    [InlineData("resources/list")]
    [InlineData("resources/read")]
    [InlineData("prompts/list")]
    [InlineData("prompts/get")]
    [InlineData("ping")]
    public async Task SendMethods_DispatchCorrectRpcMethod(string rpcMethod)
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);

        transport.SendRequestAsync(Arg.Any<JsonRpcRequest>(), Arg.Any<CancellationToken>())
            .Returns(new JsonRpcResponse { Jsonrpc = "2.0", Id = 2 });

        Func<Task> act = rpcMethod switch
        {
            "tools/list"     => () => conn.ListToolsAsync(),
            "tools/call"     => () => conn.CallToolAsync("tool"),
            "resources/list" => () => conn.ListResourcesAsync(),
            "resources/read" => () => conn.ReadResourceAsync("resource://x"),
            "prompts/list"   => () => conn.ListPromptsAsync(),
            "prompts/get"    => () => conn.GetPromptAsync("prompt"),
            "ping"           => () => conn.PingAsync(),
            _                => throw new InvalidOperationException("Unknown method in test")
        };

        await act();

        await transport.Received().SendRequestAsync(
            Arg.Is<JsonRpcRequest>(r => r.Method == rpcMethod),
            Arg.Any<CancellationToken>());
    }

    // ── DisconnectAsync ─────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_TransitionsToDisconnected()
    {
        var conn = new McpConnection();
        await conn.ConnectAsync(BuildSuccessTransport());
        await conn.DisconnectAsync();
        conn.State.Should().Be(McpConnectionState.Disconnected);
    }

    [Fact]
    public async Task DisconnectAsync_DisposesTransport()
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);
        await conn.DisconnectAsync();

        await transport.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisconnectAsync_IsSafeToCallWhenAlreadyDisconnected()
    {
        var conn = new McpConnection();
        var act = () => conn.DisconnectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AfterDisconnect_SendMethodsThrow()
    {
        var conn = new McpConnection();
        await conn.ConnectAsync(BuildSuccessTransport());
        await conn.DisconnectAsync();

        var act = () => conn.ListToolsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── DisposeAsync ────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_DisconnectsAndDisposesTransport()
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);

        await conn.DisposeAsync();

        conn.State.Should().Be(McpConnectionState.Disconnected);
        await transport.Received(1).DisposeAsync();
    }

    // ── Request ID sequencing ────────────────────────────────────

    [Fact]
    public async Task RequestIds_AreMonotonicallyIncreasing()
    {
        var conn = new McpConnection();
        var transport = BuildSuccessTransport();
        await conn.ConnectAsync(transport);

        var captured = new List<int>();
        transport.SendRequestAsync(Arg.Any<JsonRpcRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured.Add(ci.ArgAt<JsonRpcRequest>(0).Id);
                return Task.FromResult(new JsonRpcResponse { Jsonrpc = "2.0", Id = ci.ArgAt<JsonRpcRequest>(0).Id });
            });

        await conn.ListToolsAsync();
        await conn.ListResourcesAsync();
        await conn.ListPromptsAsync();

        captured.Should().BeInAscendingOrder();
        captured.Should().OnlyHaveUniqueItems();
    }
}
