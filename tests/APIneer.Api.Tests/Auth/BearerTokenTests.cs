using APIneer.Api.Auth;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// RED-phase tests for Bearer Token authentication.
/// Auth type "bearer" with token value → adds "Authorization: Bearer {token}" header.
/// All tests should FAIL until the AuthHandler is implemented.
/// </summary>
public class BearerTokenTests
{
    private readonly AuthHandler _handler = new(new HttpClient());

    [Fact]
    public async Task Bearer_AddsAuthorizationHeader()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test-token"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("Authorization");
    }

    [Fact]
    public async Task Bearer_HeaderValueContainsBearerPrefix()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = "my-access-token"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers["Authorization"].Should().Be("Bearer my-access-token");
    }

    [Fact]
    public async Task Bearer_DoesNotModifyUrl()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data?q=test" };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = "my-access-token"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Url.Should().Be("https://api.example.com/data?q=test");
    }

    [Fact]
    public async Task Bearer_OverwritesExistingAuthorizationHeader()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "https://api.example.com/data",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Basic old-value" }
        };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = "new-token"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers["Authorization"].Should().Be("Bearer new-token");
    }

    [Fact]
    public async Task Bearer_PreservesOtherHeaders()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "https://api.example.com/data",
            Headers = new Dictionary<string, string>
            {
                ["Accept"] = "application/json",
                ["X-Custom"] = "value"
            }
        };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = "my-token"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers["Accept"].Should().Be("application/json");
        request.Headers["X-Custom"].Should().Be("value");
        request.Headers["Authorization"].Should().Be("Bearer my-token");
    }

    [Fact]
    public async Task Bearer_EmptyToken_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = ""
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Bearer_NullToken_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "bearer",
            Token = null
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
