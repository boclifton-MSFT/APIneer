using APIneer.Api.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace APIneer.Api.Tests.Mcp;

/// <summary>
/// Tests for McpConnectionManager: connection tracking, transport factory, and disposal.
/// </summary>
public class McpConnectionManagerTests : IAsyncDisposable
{
    private readonly McpConnectionManager _manager = new(NullLoggerFactory.Instance);

    public async ValueTask DisposeAsync() => await _manager.DisposeAsync();

    // ── CreateConnection ─────────────────────────────────────────

    [Fact]
    public void CreateConnection_ReturnsDisconnectedConnection()
    {
        var conn = _manager.CreateConnection();

        conn.Should().NotBeNull();
        conn.State.Should().Be(McpConnectionState.Disconnected);
    }

    [Fact]
    public void CreateConnection_AssignsNonEmptyGuid()
    {
        var conn = _manager.CreateConnection();
        conn.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateConnection_AssignsUniqueIdsEachTime()
    {
        var ids = Enumerable.Range(0, 10)
            .Select(_ => _manager.CreateConnection().Id)
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    // ── GetConnection ────────────────────────────────────────────

    [Fact]
    public void GetConnection_ReturnsConnection_WhenIdExists()
    {
        var conn = _manager.CreateConnection();
        var found = _manager.GetConnection(conn.Id);
        found.Should().BeSameAs(conn);
    }

    [Fact]
    public void GetConnection_ReturnsNull_ForUnknownId()
    {
        _manager.GetConnection(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetConnection_ReturnsNull_ForEmptyGuid()
    {
        _manager.GetConnection(Guid.Empty).Should().BeNull();
    }

    // ── GetAllConnections ────────────────────────────────────────

    [Fact]
    public void GetAllConnections_ReturnsEmpty_WhenNoConnectionsCreated()
    {
        _manager.GetAllConnections().Should().BeEmpty();
    }

    [Fact]
    public void GetAllConnections_ReturnsAllCreatedConnections()
    {
        var a = _manager.CreateConnection();
        var b = _manager.CreateConnection();
        var c = _manager.CreateConnection();

        _manager.GetAllConnections().Should().BeEquivalentTo(new[] { a, b, c });
    }

    [Fact]
    public void GetAllConnections_ReturnsSnapshot_NotLiveView()
    {
        _manager.CreateConnection();
        var snapshot = _manager.GetAllConnections();

        _manager.CreateConnection();

        // The snapshot captured before the second Create should have only 1 item
        snapshot.Should().HaveCount(1);
    }

    // ── RemoveConnectionAsync ────────────────────────────────────

    [Fact]
    public async Task RemoveConnectionAsync_ReturnsTrue_WhenConnectionExists()
    {
        var conn = _manager.CreateConnection();
        var result = await _manager.RemoveConnectionAsync(conn.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveConnectionAsync_ReturnsFalse_ForUnknownId()
    {
        var result = await _manager.RemoveConnectionAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveConnectionAsync_RemovesFromTracking()
    {
        var conn = _manager.CreateConnection();
        await _manager.RemoveConnectionAsync(conn.Id);

        _manager.GetConnection(conn.Id).Should().BeNull();
        _manager.GetAllConnections().Should().NotContain(conn);
    }

    [Fact]
    public async Task RemoveConnectionAsync_IdempotentOnSecondCall()
    {
        var conn = _manager.CreateConnection();

        var first = await _manager.RemoveConnectionAsync(conn.Id);
        var second = await _manager.RemoveConnectionAsync(conn.Id);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveConnectionAsync_OnlyRemovesTargetedConnection()
    {
        var keep = _manager.CreateConnection();
        var remove = _manager.CreateConnection();

        await _manager.RemoveConnectionAsync(remove.Id);

        _manager.GetConnection(keep.Id).Should().BeSameAs(keep);
        _manager.GetAllConnections().Should().ContainSingle().Which.Should().BeSameAs(keep);
    }

    // ── CreateTransport — validation ─────────────────────────────

    [Fact]
    public void CreateTransport_ThrowsArgumentException_ForUnknownTransportType()
    {
        var act = () => _manager.CreateTransport("websocket", null, null, null, null);
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown transport type*");
    }

    [Fact]
    public void CreateTransport_ThrowsArgumentException_ForStdioWithoutCommand()
    {
        var act = () => _manager.CreateTransport("stdio", command: null, args: null, envVars: null, url: null);
        act.Should().Throw<ArgumentException>().WithMessage("*Command is required*");
    }

    [Fact]
    public void CreateTransport_ThrowsArgumentException_ForStreamableHttpWithoutUrl()
    {
        var act = () => _manager.CreateTransport("streamable-http", command: null, args: null, envVars: null, url: null);
        act.Should().Throw<ArgumentException>().WithMessage("*URL is required*");
    }

    [Fact]
    public async Task CreateTransport_ReturnsHttpMcpTransport_ForStreamableHttp()
    {
        var transport = _manager.CreateTransport(
            "streamable-http", command: null, args: null, envVars: null,
            url: "http://localhost:3000/mcp");

        transport.Should().BeOfType<HttpMcpTransport>();
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task CreateTransport_StreamableHttp_AcceptsCustomHeaders()
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" };
        var transport = _manager.CreateTransport(
            "streamable-http", command: null, args: null, envVars: null,
            url: "http://localhost:3000/mcp", headers: headers);

        transport.Should().BeOfType<HttpMcpTransport>();
        await transport.DisposeAsync();
    }

    // ── DisposeAsync ────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_ClearsAllConnections()
    {
        // Use a fresh manager so disposing it doesn't interfere with the class-level one
        var mgr = new McpConnectionManager(NullLoggerFactory.Instance);
        mgr.CreateConnection();
        mgr.CreateConnection();

        await mgr.DisposeAsync();

        mgr.GetAllConnections().Should().BeEmpty();
    }
}
