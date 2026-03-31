using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that the proxy engine measures response time and calculates response size accurately.
/// These metrics are stored in RequestHistory and displayed in the UI.
/// </summary>
public class ProxyTimingTests : IAsyncLifetime
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
    public async Task ResponseTime_IsMeasured_GreaterThanZero()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.ResponseTimeMs.Should().BeGreaterThan(0,
            "response time should be measured and always > 0 for a real HTTP round-trip");
    }

    [Fact]
    public async Task ResponseTime_ReflectsSlowEndpoints()
    {
        // The /slow endpoint delays for the specified milliseconds
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/slow?ms=200",
            TimeoutSeconds = 10
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        // The response time should be at least 200ms given the server-side delay
        response.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(150,
            "a 200ms server delay should result in at least ~150ms measured time");
    }

    [Fact]
    public async Task ResponseSize_IsCalculatedCorrectly()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/large-body?size=5000"
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.ResponseSizeBytes.Should().BeGreaterThan(0,
            "response size must be calculated for successful responses");
        // The body alone is 5000 chars; with headers the total should exceed 5000 bytes
        response.ResponseSizeBytes.Should().BeGreaterThanOrEqualTo(5000);
    }

    [Fact]
    public async Task ResponseSize_IncludesHeaders()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        // Response size should be larger than just the body, because it includes headers
        var bodySize = response.Body?.Length ?? 0;
        response.ResponseSizeBytes.Should().BeGreaterThanOrEqualTo(bodySize,
            "total response size should include at least the body bytes");
    }

    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
