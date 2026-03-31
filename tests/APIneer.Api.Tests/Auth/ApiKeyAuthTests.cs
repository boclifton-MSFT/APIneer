using APIneer.Api.Auth;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// RED-phase tests for API Key authentication.
/// Auth type "api_key" with key name, key value, and placement (header or query).
/// All tests should FAIL until the AuthHandler is implemented.
/// </summary>
public class ApiKeyAuthTests
{
    private readonly AuthHandler _handler = new(new HttpClient());

    // ── Header Placement ────────────────────────────────────────

    [Fact]
    public async Task ApiKey_HeaderPlacement_InjectsCustomHeader()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Api-Key",
            KeyValue = "secret-key-123",
            Placement = "header"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("X-Api-Key");
        request.Headers["X-Api-Key"].Should().Be("secret-key-123");
    }

    [Fact]
    public async Task ApiKey_HeaderPlacement_DoesNotModifyUrl()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Api-Key",
            KeyValue = "secret-key-123",
            Placement = "header"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Url.Should().Be("https://api.example.com/data");
    }

    [Fact]
    public async Task ApiKey_HeaderPlacement_PreservesExistingHeaders()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = "https://api.example.com/data",
            Headers = new Dictionary<string, string> { ["Accept"] = "application/json" }
        };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Api-Key",
            KeyValue = "secret-key-123",
            Placement = "header"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("Accept");
        request.Headers["Accept"].Should().Be("application/json");
        request.Headers.Should().ContainKey("X-Api-Key");
    }

    // ── Query Placement ─────────────────────────────────────────

    [Fact]
    public async Task ApiKey_QueryPlacement_AppendsToUrl()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "api_key",
            KeyValue = "secret-key-123",
            Placement = "query"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Url.Should().Contain("api_key=secret-key-123");
    }

    [Fact]
    public async Task ApiKey_QueryPlacement_PreservesExistingQueryParams()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data?page=1" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "api_key",
            KeyValue = "secret-key-123",
            Placement = "query"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Url.Should().Contain("page=1");
        request.Url.Should().Contain("api_key=secret-key-123");
        request.Url.Should().Contain("&");
    }

    [Fact]
    public async Task ApiKey_QueryPlacement_DoesNotAddHeader()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "api_key",
            KeyValue = "secret-key-123",
            Placement = "query"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().NotContainKey("api_key");
    }

    [Fact]
    public async Task ApiKey_QueryPlacement_UrlEncodesSpecialCharacters()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "key",
            KeyValue = "value with spaces&special=chars",
            Placement = "query"
        };

        await _handler.ApplyAuthAsync(request, auth);

        // The value should be URL-encoded
        request.Url.Should().NotContain("value with spaces");
        request.Url.Should().Contain("key=");
    }

    // ── Validation ──────────────────────────────────────────────

    [Fact]
    public async Task ApiKey_MissingKeyName_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = null,
            KeyValue = "secret-key-123",
            Placement = "header"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApiKey_MissingKeyValue_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Api-Key",
            KeyValue = null,
            Placement = "header"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApiKey_MissingPlacement_DefaultsToHeader()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Api-Key",
            KeyValue = "secret-key-123",
            Placement = null
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("X-Api-Key");
        request.Headers["X-Api-Key"].Should().Be("secret-key-123");
    }
}
