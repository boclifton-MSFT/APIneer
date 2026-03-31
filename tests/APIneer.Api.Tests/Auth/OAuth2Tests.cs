using System.Net;
using System.Text;
using APIneer.Api.Auth;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// RED-phase tests for OAuth 2.0 Client Credentials authentication.
/// Auth type "oauth2" with token endpoint, client ID, client secret, scope.
/// Tests cover: token acquisition, bearer injection, refresh, and error handling.
/// All tests should FAIL until the AuthHandler is implemented.
/// </summary>
public class OAuth2Tests : IDisposable
{
    private readonly MockTokenEndpointHandler _mockHandler = new();
    private readonly AuthHandler _handler;

    public OAuth2Tests()
    {
        var httpClient = new HttpClient(_mockHandler);
        _handler = new AuthHandler(httpClient);
    }

    public void Dispose()
    {
        _mockHandler.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Token Acquisition ───────────────────────────────────────

    [Fact]
    public async Task OAuth2_PostsToTokenEndpoint()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read write"
        };

        await _handler.ApplyAuthAsync(request, auth);

        _mockHandler.LastRequest.Should().NotBeNull();
        _mockHandler.LastRequest!.RequestUri!.ToString().Should().Be("https://auth.example.com/oauth/token");
        _mockHandler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task OAuth2_SendsClientCredentialsGrant()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read write"
        };

        await _handler.ApplyAuthAsync(request, auth);

        var body = _mockHandler.LastRequestBody;
        body.Should().NotBeNull();
        body.Should().Contain("grant_type=client_credentials");
        body.Should().Contain("client_id=my-client-id");
        body.Should().Contain("client_secret=my-client-secret");
    }

    [Fact]
    public async Task OAuth2_IncludesScopeInTokenRequest()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read write"
        };

        await _handler.ApplyAuthAsync(request, auth);

        _mockHandler.LastRequestBody.Should().Contain("scope=read");
    }

    [Fact]
    public async Task OAuth2_AddsAccessTokenAsBearerHeader()
    {
        _mockHandler.ResponseJson = """{"access_token":"test-access-token-xyz","token_type":"bearer","expires_in":3600}""";

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("Authorization");
        request.Headers["Authorization"].Should().Be("Bearer test-access-token-xyz");
    }

    [Fact]
    public async Task OAuth2_StoresAccessTokenInAuthConfig()
    {
        _mockHandler.ResponseJson = """{"access_token":"fetched-token","token_type":"bearer","expires_in":3600}""";

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read"
        };

        await _handler.ApplyAuthAsync(request, auth);

        auth.AccessToken.Should().Be("fetched-token");
    }

    // ── Token Refresh ───────────────────────────────────────────

    [Fact]
    public async Task OAuth2_UsesExistingTokenWhenNotExpired()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read",
            AccessToken = "cached-token",
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        await _handler.ApplyAuthAsync(request, auth);

        // Should use the cached token, not call the endpoint
        _mockHandler.LastRequest.Should().BeNull();
        request.Headers["Authorization"].Should().Be("Bearer cached-token");
    }

    [Fact]
    public async Task OAuth2_RefreshesExpiredToken()
    {
        _mockHandler.ResponseJson = """{"access_token":"refreshed-token","token_type":"bearer","expires_in":3600}""";

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            Scope = "read",
            AccessToken = "old-expired-token",
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired
        };

        await _handler.ApplyAuthAsync(request, auth);

        _mockHandler.LastRequest.Should().NotBeNull("expired token should trigger a new token request");
        request.Headers["Authorization"].Should().Be("Bearer refreshed-token");
        auth.AccessToken.Should().Be("refreshed-token");
    }

    [Fact]
    public async Task OAuth2_SetsTokenExpiresAt()
    {
        _mockHandler.ResponseJson = """{"access_token":"new-token","token_type":"bearer","expires_in":7200}""";

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret"
        };

        var before = DateTime.UtcNow;
        await _handler.ApplyAuthAsync(request, auth);

        auth.TokenExpiresAt.Should().NotBeNull();
        auth.TokenExpiresAt!.Value.Should().BeAfter(before.AddSeconds(7100));
    }

    // ── Error Handling ──────────────────────────────────────────

    [Fact]
    public async Task OAuth2_TokenEndpointReturnsError_ThrowsInvalidOperationException()
    {
        _mockHandler.ResponseStatusCode = HttpStatusCode.BadRequest;
        _mockHandler.ResponseJson = """{"error":"invalid_client","error_description":"Client authentication failed"}""";

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "bad-client",
            ClientSecret = "bad-secret"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*token*");
    }

    [Fact]
    public async Task OAuth2_MissingTokenEndpoint_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = null,
            ClientId = "my-client",
            ClientSecret = "my-secret"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OAuth2_MissingClientId_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = null,
            ClientSecret = "my-secret"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OAuth2_MissingClientSecret_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "my-client",
            ClientSecret = null
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Mock Infrastructure ─────────────────────────────────────

    /// <summary>
    /// Mock HTTP handler that simulates an OAuth2 token endpoint.
    /// Captures the last request for assertions.
    /// </summary>
    private class MockTokenEndpointHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseJson { get; set; } =
            """{"access_token":"mock-access-token","token_type":"bearer","expires_in":3600}""";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(ResponseStatusCode)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
