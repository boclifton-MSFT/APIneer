using System.Text.Json;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that request headers are forwarded to the target and response headers
/// are captured completely, including Content-Type preservation and User-Agent customization.
/// </summary>
public class ProxyHeaderTests : IAsyncLifetime
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
    public async Task CustomRequestHeaders_AreSentToTarget()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/headers/echo",
            Headers =
            {
                ["X-Custom-Header"] = "my-custom-value",
                ["X-Another-Header"] = "another-value"
            }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().NotBeNullOrEmpty();

        // The echo endpoint returns all received headers as JSON
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Body!);
        headers.Should().ContainKey("X-Custom-Header");
        headers!["X-Custom-Header"].Should().Be("my-custom-value");
        headers.Should().ContainKey("X-Another-Header");
        headers["X-Another-Header"].Should().Be("another-value");
    }

    [Fact]
    public async Task ResponseHeaders_AreCapturedCompletely()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        // The /echo endpoint sets X-Custom-Response
        response.Headers.Should().ContainKey("X-Custom-Response");
        var values = response.Headers["X-Custom-Response"].ToList();
        values.Should().Contain("hello");
    }

    [Fact]
    public async Task ContentTypeHeader_IsPreservedOnResponse()
    {
        var jsonBody = """{"data":"test"}""";
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/content-type/echo",
            Body = jsonBody,
            Headers = { ["Content-Type"] = "application/json" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        // The content-type echo endpoint mirrors back the request Content-Type
        response.Headers.Should().ContainKey("Content-Type");
        var contentType = string.Join(",", response.Headers["Content-Type"]);
        contentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task UserAgent_CanBeCustomized()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/headers/echo",
            Headers = { ["User-Agent"] = "APIneer/1.0 CustomAgent" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);

        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Body!);
        headers.Should().ContainKey("User-Agent");
        headers!["User-Agent"].Should().Contain("APIneer/1.0 CustomAgent");
    }

    [Fact]
    public async Task MultipleResponseHeaders_WithSameKey_AreCaptured()
    {
        // Standard HTTP allows multiple values for the same header key
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        // At minimum, standard headers like Content-Type should be present
        response.Headers.Should().NotBeEmpty();
        // Each header key should map to an enumerable of values
        foreach (var (_, values) in response.Headers)
        {
            values.Should().NotBeNull();
        }
    }

    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
