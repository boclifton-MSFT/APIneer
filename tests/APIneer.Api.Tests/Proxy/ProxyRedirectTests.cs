using System.Text.Json;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that the proxy engine handles HTTP redirects correctly:
/// follows by default, captures redirect chains, and can disable following.
/// </summary>
public class ProxyRedirectTests : IAsyncLifetime
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
    public async Task Redirects_AreFollowedByDefault()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/redirect/single"
            // FollowRedirects defaults to true
        };

        var response = await _proxy.SendAsync(request);

        // Should follow the redirect and return the final response
        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("GET echo");
    }

    [Fact]
    public async Task RedirectChain_IsCapturedWhenConfigured()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/redirect/start",
            CaptureRedirectChain = true
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("final destination");

        // The chain should record: /redirect/start → /redirect/middle → /redirect/end
        response.RedirectChain.Should().NotBeNull();
        response.RedirectChain.Should().HaveCountGreaterThanOrEqualTo(2,
            "there are at least 2 redirect hops before the final destination");

        // Each entry should have a URL and status code
        foreach (var entry in response.RedirectChain!)
        {
            entry.Url.Should().NotBeNullOrEmpty();
            entry.StatusCode.Should().BeInRange(300, 399,
                "redirect entries should have 3xx status codes");
        }
    }

    [Fact]
    public async Task RedirectChain_IsNullWhenNotConfigured()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/redirect/single",
            CaptureRedirectChain = false
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        // When CaptureRedirectChain is false, no chain should be recorded
        response.RedirectChain.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Redirects_CanBeDisabled()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/redirect/single",
            FollowRedirects = false
        };

        var response = await _proxy.SendAsync(request);

        // Should return the redirect response itself, not follow it
        response.StatusCode.Should().BeInRange(300, 399,
            "when redirects are disabled, the raw 3xx response should be returned");
        response.Headers.Should().ContainKey("Location",
            "redirect responses include a Location header");
    }

    [Fact]
    public async Task MultipleRedirects_AllFollowed()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/redirect/start"
            // FollowRedirects defaults to true
        };

        var response = await _proxy.SendAsync(request);

        // Should follow through all redirects to the final destination
        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("final destination");
    }

    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
