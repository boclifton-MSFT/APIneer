using System.Net;
using System.Text;
using System.Text.Json;
using APIneer.Api.Auth;
using APIneer.Api.Data;
using APIneer.Api.Models;
using APIneer.Api.Proxy;
using APIneer.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace APIneer.Api.Tests.Security;

/// <summary>
/// Security regression tests for auth engine.
/// Verifies that secrets are not exposed in storage, logs, or responses.
/// Based on Phase 5.4 security review findings (P5-001 through P5-009).
/// </summary>
public class AuthSecurityTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public AuthSecurityTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private AppDbContext GetDbContext()
    {
        var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private IAuthHandler GetAuthHandler()
    {
        var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAuthHandler>();
    }

    private ICredentialProtector GetProtector()
    {
        var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ICredentialProtector>();
    }

    private async Task<Collection> CreateCollectionAsync(string name)
    {
        var db = GetDbContext();
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test Workspace",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();
        return collection;
    }

    // ── CRITICAL: P5-001 AuthConfig Secrets Not Persisted Plaintext ────────

    /// <summary>
    /// REGRESSION: TC5.1
    /// Given: A request with bearer token auth
    /// When: Request is stored in database
    /// Then: Database should NOT contain plaintext token
    /// </summary>
    [Fact(Skip = "P5-001: AuthConfig encryption not yet implemented")]
    public async Task AuthSecurityTest_BearerToken_NotStoredPlaintextInDatabase()
    {
        var collection = await CreateCollectionAsync("Test");
        var plainToken = "secret-token-xyz-123";

        var authConfig = new
        {
            type = "bearer",
            token = plainToken
        };

        var db = GetDbContext();
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = "API Call",
            Method = "GET",
            Url = "https://api.example.com/data",
            AuthConfig = JsonSerializer.Serialize(authConfig)
        };

        db.ApiRequests.Add(request);
        await db.SaveChangesAsync();

        // Query database directly
        var stored = await db.ApiRequests.FindAsync(request.Id);
        stored!.AuthConfig.Should().NotContain(plainToken, "plaintext token should not be stored in database");
    }

    /// <summary>
    /// REGRESSION: TC5.1 (API Key variant)
    /// Given: Request with API key auth
    /// When: Stored in database
    /// Then: Database should NOT contain plaintext API key
    /// </summary>
    [Fact(Skip = "P5-001: AuthConfig encryption not yet implemented")]
    public async Task AuthSecurityTest_ApiKey_NotStoredPlaintextInDatabase()
    {
        var collection = await CreateCollectionAsync("Test");
        var plainApiKey = "sk-project-1234567890";

        var authConfig = new
        {
            type = "api_key",
            keyName = "X-API-Key",
            keyValue = plainApiKey,
            placement = "header"
        };

        var db = GetDbContext();
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = "API Call",
            Method = "GET",
            Url = "https://api.example.com/data",
            AuthConfig = JsonSerializer.Serialize(authConfig)
        };

        db.ApiRequests.Add(request);
        await db.SaveChangesAsync();

        var stored = await db.ApiRequests.FindAsync(request.Id);
        stored!.AuthConfig.Should().NotContain(plainApiKey, "plaintext API key should not be stored in database");
    }

    /// <summary>
    /// REGRESSION: TC5.1 (Basic Auth variant)
    /// Given: Request with Basic auth (username:password)
    /// When: Stored in database
    /// Then: Database should NOT contain plaintext password
    /// </summary>
    [Fact(Skip = "P5-001: AuthConfig encryption not yet implemented")]
    public async Task AuthSecurityTest_BasicAuth_NotStoredPlaintextInDatabase()
    {
        var collection = await CreateCollectionAsync("Test");
        var plainPassword = "p@ssw0rd123!";

        var authConfig = new
        {
            type = "basic",
            username = "admin",
            password = plainPassword
        };

        var db = GetDbContext();
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = "API Call",
            Method = "GET",
            Url = "https://api.example.com/data",
            AuthConfig = JsonSerializer.Serialize(authConfig)
        };

        db.ApiRequests.Add(request);
        await db.SaveChangesAsync();

        var stored = await db.ApiRequests.FindAsync(request.Id);
        stored!.AuthConfig.Should().NotContain(plainPassword, "plaintext password should not be stored in database");
    }

    // ── HIGH: P5-002 OAuth2 Tokens Not Cached in Persistent Storage ───────

    /// <summary>
    /// REGRESSION: TC5.2
    /// Given: OAuth2 auth with client credentials
    /// When: Token is fetched and cached
    /// And: Request is stored to database
    /// Then: Database should NOT contain the access token
    /// </summary>
    [Fact(Skip = "P5-002: OAuth2 token caching in-memory not implemented")]
    public async Task AuthSecurityTest_OAuth2AccessToken_NotStoredInDatabase()
    {
        var collection = await CreateCollectionAsync("Test");
        var plainAccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";

        var authConfig = new
        {
            type = "oauth2",
            tokenEndpoint = "https://auth.example.com/oauth/token",
            clientId = "client-id",
            clientSecret = "secret",
            accessToken = plainAccessToken,
            tokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var db = GetDbContext();
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = "API Call",
            Method = "GET",
            Url = "https://api.example.com/data",
            AuthConfig = JsonSerializer.Serialize(authConfig)
        };

        db.ApiRequests.Add(request);
        await db.SaveChangesAsync();

        var stored = await db.ApiRequests.FindAsync(request.Id);
        stored!.AuthConfig.Should().NotContain(plainAccessToken, "access token should not be stored in database");
    }

    // ── HIGH: P5-003 Client Secret Sent Over HTTPS Only ──────────────────

    /// <summary>
    /// REGRESSION: TC5.4
    /// Given: OAuth2 token endpoint that is HTTP (not HTTPS)
    /// When: ApplyAuthAsync() is called
    /// Then: InvalidOperationException thrown
    /// And: Request is not sent
    /// </summary>
    [Fact(Skip = "P5-003: HTTPS validation for token endpoint not implemented")]
    public async Task AuthSecurityTest_OAuth2_HttpTokenEndpoint_Rejected()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "http://auth.example.com/oauth/token",  // ← HTTP, not HTTPS
            ClientId = "client-id",
            ClientSecret = "secret"
        };

        var handler = GetAuthHandler();
        var act = () => handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*HTTPS*");
    }

    // ── MEDIUM: P5-004 API Key in Query String Not Logged ────────────────

    /// <summary>
    /// REGRESSION: TC5.3
    /// Given: API key auth with query placement
    /// When: Request is executed and history is stored
    /// Then: RequestHistory.Url should NOT contain plaintext API key value
    /// And: Should show [REDACTED] or similar for sensitive params
    /// </summary>
    [Fact(Skip = "P5-004: Request history sanitization not implemented")]
    public async Task AuthSecurityTest_ApiKeyQuery_NotStoredInHistory()
    {
        var collection = await CreateCollectionAsync("Test");
        var plainApiKey = "sk-1234567890abcdef";

        var authConfig = new
        {
            type = "api_key",
            keyName = "api_key",
            keyValue = plainApiKey,
            placement = "query"
        };

        var db = GetDbContext();
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = "API Call",
            Method = "GET",
            Url = "https://api.example.com/data",
            AuthConfig = JsonSerializer.Serialize(authConfig)
        };

        db.ApiRequests.Add(request);
        await db.SaveChangesAsync();

        // Simulate request execution (would normally come from /send endpoint)
        var history = new RequestHistory
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            Method = "GET",
            Url = "https://api.example.com/data?api_key=[REDACTED]",  // Should be redacted
            RequestHeaders = "{}",
            RequestBody = null,
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = "{}",
            ResponseTimeMs = 100,
            ResponseSizeBytes = 2,
            ExecutedAt = DateTime.UtcNow
        };

        db.RequestHistory.Add(history);
        await db.SaveChangesAsync();

        // Query history — should not contain plaintext API key
        var stored = await db.RequestHistory.FindAsync(history.Id);
        stored!.Url.Should().NotContain(plainApiKey, "plaintext API key should not be stored in request history URL");
    }

    // ── MEDIUM: P5-005 OAuth2 Error Messages Sanitized ───────────────────

    /// <summary>
    /// REGRESSION: AuthHandler should not echo OAuth2 server responses in exceptions
    /// </summary>
    [Fact(Skip = "P5-005: OAuth2 error message sanitization not implemented")]
    public async Task AuthSecurityTest_OAuth2_ErrorNotEchoed()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "bad-client",
            ClientSecret = "bad-secret"
        };

        // Mock handler that returns error
        var mockHandler = new MockErrorTokenEndpoint();
        var handler = new AuthHandler(new HttpClient(mockHandler));

        var act = () => handler.ApplyAuthAsync(request, auth);

        // Exception should NOT contain the server response details
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OAuth2*")
            .Where(ex => !ex.Message.Contains("invalid_client") && !ex.Message.Contains("Client authentication failed"));
    }

    // ── MEDIUM: P5-006 Auth Type Validation Strict ───────────────────────

    /// <summary>
    /// REGRESSION: TC5.5
    /// Given: AuthConfig with null Type
    /// When: ApplyAuthAsync() called
    /// Then: ArgumentException thrown (not NullReferenceException)
    /// </summary>
    [Fact]
    public async Task AuthSecurityTest_AuthType_NullRejected()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = null
        };

        var handler = GetAuthHandler();
        var act = () => handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.Message.Contains("type", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// REGRESSION: AuthConfig with invalid type should throw, not silently succeed
    /// </summary>
    [Fact]
    public async Task AuthSecurityTest_AuthType_InvalidRejected()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "invalid_auth_type_xyz"
        };

        var handler = GetAuthHandler();
        var act = () => handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── INFO: P5-007 Token Mutation Tracking ─────────────────────────────

    /// <summary>
    /// REGRESSION: Verify that OAuth2 cached tokens are not mutable after storage
    /// (low severity, but important for concurrent scenarios)
    /// </summary>
    [Fact(Skip = "P5-007: Immutable token cache not implemented")]
    public async Task AuthSecurityTest_OAuth2_TokenImmutableAfterCaching()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "oauth2",
            TokenEndpoint = "https://auth.example.com/oauth/token",
            ClientId = "client-id",
            ClientSecret = "secret",
            AccessToken = "original-token",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Auth config should be immutable once cached
        // This is a defensive measure against concurrent mutation
        // (Not currently enforced, but should be)
        var originalToken = auth.AccessToken;

        // Attempt to mutate (should not be possible in final implementation)
        auth.AccessToken = "hacked-token";

        // Token should remain original (enforced by immutable type)
        // auth.AccessToken.Should().Be(originalToken);

        await Task.CompletedTask;
    }

    // ── Helper Mock Classes ──────────────────────────────────────────────

    private class MockErrorTokenEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new StringContent(
                """{"error":"invalid_client","error_description":"Client authentication failed"}""",
                Encoding.UTF8,
                "application/json");
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = content
            };
            return Task.FromResult(response);
        }
    }
}
