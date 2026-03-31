using System.Text.Json;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that the proxy engine correctly handles various body types:
/// JSON, form-data, raw text, empty bodies, and large payloads (under 10 MB limit).
/// </summary>
public class ProxyBodyTests : IAsyncLifetime
{
    private readonly TestHttpServer _server = new();
    private IProxyEngine _proxy = null!;

    public async Task InitializeAsync()
    {
        await _server.StartWithDefaults();
        _proxy = CreateProxyEngine();
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task JsonBody_SentAndReceivedCorrectly()
    {
        var jsonPayload = """{"user":"alice","role":"admin","active":true}""";
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/echo",
            Body = jsonPayload,
            Headers = { ["Content-Type"] = "application/json" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().NotBeNullOrEmpty();

        var result = JsonDocument.Parse(response.Body!);
        var receivedBody = result.RootElement.GetProperty("receivedBody").GetString();
        receivedBody.Should().NotBeNullOrEmpty();

        // Verify the JSON round-trips correctly
        var parsed = JsonDocument.Parse(receivedBody!);
        parsed.RootElement.GetProperty("user").GetString().Should().Be("alice");
        parsed.RootElement.GetProperty("role").GetString().Should().Be("admin");
        parsed.RootElement.GetProperty("active").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FormDataBody_Works()
    {
        // Simulate URL-encoded form data
        var formBody = "username=alice&password=secret123";
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/form",
            Body = formBody,
            Headers = { ["Content-Type"] = "application/x-www-form-urlencoded" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().NotBeNullOrEmpty();

        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Body!);
        result.Should().ContainKey("username");
        result!["username"].Should().Be("alice");
    }

    [Fact]
    public async Task RawTextBody_Works()
    {
        var textBody = "Hello, this is raw text content for the API.";
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/content-type/echo",
            Body = textBody,
            Headers = { ["Content-Type"] = "text/plain" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be(textBody);
    }

    [Fact]
    public async Task EmptyBody_OnGet_IsAccepted()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
            // No body — GET should not require one
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LargeBody_WithinLimit_Works()
    {
        // 1 MB of data — well within the 10 MB limit
        var largeBody = new string('A', 1_000_000);
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/content-type/echo",
            Body = largeBody,
            Headers = { ["Content-Type"] = "text/plain" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().HaveLength(1_000_000);
    }

    [Fact]
    public async Task NullBody_OnPost_DoesNotCrash()
    {
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/echo",
            Body = null
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.IsSuccess.Should().BeTrue();
    }

    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
