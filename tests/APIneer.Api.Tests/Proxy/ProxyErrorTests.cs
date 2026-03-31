using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that the proxy engine returns structured errors instead of throwing exceptions.
/// Covers: timeout, invalid URL, connection refused, and DNS resolution failure.
/// Per security-architecture.md, the proxy returns renderable results — never crashes.
/// </summary>
public class ProxyErrorTests : IAsyncLifetime
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
    public async Task Timeout_ReturnsStructuredError()
    {
        // Hit the slow endpoint with a 1-second timeout (endpoint delays 5s by default)
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/slow?ms=5000",
            TimeoutSeconds = 1
        };

        var response = await _proxy.SendAsync(request);

        response.IsSuccess.Should().BeFalse("a timeout should be reported as a failure");
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be("TIMEOUT");
        response.Error.Message.Should().NotBeNullOrEmpty();
        response.StatusCode.Should().Be(0, "no HTTP status is received on timeout");
    }

    [Fact]
    public async Task InvalidUrl_ReturnsStructuredError()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "not-a-valid-url"
        };

        var response = await _proxy.SendAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be("INVALID_URL");
        response.Error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConnectionRefused_ReturnsStructuredError()
    {
        // Port 1 is almost certainly not listening on localhost
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "http://127.0.0.1:1/should-refuse",
            TimeoutSeconds = 5
        };

        var response = await _proxy.SendAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be("CONNECTION_REFUSED");
        response.Error.Message.Should().NotBeNullOrEmpty();
        response.StatusCode.Should().Be(0);
    }

    [Fact]
    public async Task DnsResolutionFailure_ReturnsStructuredError()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "http://this-domain-does-not-exist-apineer-test-12345.invalid/api",
            TimeoutSeconds = 10
        };

        var response = await _proxy.SendAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be("DNS_FAILURE");
        response.Error.Message.Should().NotBeNullOrEmpty();
        response.StatusCode.Should().Be(0);
    }

    [Fact]
    public async Task Error_NeverThrowsException()
    {
        // Even the worst-case input should return a ProxyResponse, not throw
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = ""
        };

        var act = () => _proxy.SendAsync(request);

        // The contract says: never throw for transport errors
        var proxyResponse = await act.Invoke();
        proxyResponse.IsSuccess.Should().BeFalse();
        proxyResponse.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Error_IncludesResponseTimeEvenOnFailure()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "http://this-domain-does-not-exist-apineer-test-12345.invalid/api",
            TimeoutSeconds = 5
        };

        var response = await _proxy.SendAsync(request);

        // Even failed requests should report how long we waited
        response.ResponseTimeMs.Should().BeGreaterThan(0,
            "time spent attempting the request should be captured even on failure");
    }

    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
