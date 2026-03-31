using APIneer.Api.Auth;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// RED-phase tests for auth inheritance between collection and request levels.
/// Resolution order per security-architecture.md: Request > Collection > None.
/// Auth type "none" explicitly disables inherited auth.
/// All tests should FAIL until the AuthHandler is implemented.
/// </summary>
public class AuthInheritanceTests
{
    private readonly AuthHandler _handler = new(new HttpClient());

    // ── Inheritance from collection ─────────────────────────────

    [Fact]
    public void ResolveAuth_RequestWithNoAuth_InheritsFromCollection()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "bearer",
            Token = "collection-token"
        };

        var resolved = _handler.ResolveAuth(requestAuth: null, collectionAuth: collectionAuth);

        resolved.Should().NotBeNull();
        resolved!.Type.Should().Be("bearer");
        resolved.Token.Should().Be("collection-token");
    }

    [Fact]
    public void ResolveAuth_RequestWithOwnAuth_OverridesCollection()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "bearer",
            Token = "collection-token"
        };
        var requestAuth = new AuthConfig
        {
            Type = "api_key",
            KeyName = "X-Key",
            KeyValue = "request-key",
            Placement = "header"
        };

        var resolved = _handler.ResolveAuth(requestAuth: requestAuth, collectionAuth: collectionAuth);

        resolved.Should().NotBeNull();
        resolved!.Type.Should().Be("api_key");
        resolved.KeyValue.Should().Be("request-key");
    }

    [Fact]
    public void ResolveAuth_CollectionWithNoAuth_RequestHasNoAuth()
    {
        var resolved = _handler.ResolveAuth(requestAuth: null, collectionAuth: null);

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveAuth_AuthTypeNone_ExplicitlyDisablesInheritedAuth()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "bearer",
            Token = "collection-token"
        };
        var requestAuth = new AuthConfig
        {
            Type = "none"
        };

        var resolved = _handler.ResolveAuth(requestAuth: requestAuth, collectionAuth: collectionAuth);

        resolved.Should().BeNull("type 'none' means no auth, overriding collection");
    }

    [Fact]
    public void ResolveAuth_RequestAuthSameTypeAsCollection_UsesRequestValues()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "basic",
            Username = "collection-user",
            Password = "collection-pass"
        };
        var requestAuth = new AuthConfig
        {
            Type = "basic",
            Username = "request-user",
            Password = "request-pass"
        };

        var resolved = _handler.ResolveAuth(requestAuth: requestAuth, collectionAuth: collectionAuth);

        resolved.Should().NotBeNull();
        resolved!.Username.Should().Be("request-user");
        resolved.Password.Should().Be("request-pass");
    }

    // ── End-to-end: resolve then apply ──────────────────────────

    [Fact]
    public async Task InheritedAuth_AppliedToProxyRequest()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "bearer",
            Token = "inherited-token"
        };

        var resolved = _handler.ResolveAuth(requestAuth: null, collectionAuth: collectionAuth);
        resolved.Should().NotBeNull("should inherit from collection");

        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        await _handler.ApplyAuthAsync(request, resolved!);

        request.Headers.Should().ContainKey("Authorization");
        request.Headers["Authorization"].Should().Be("Bearer inherited-token");
    }

    [Fact]
    public async Task ExplicitNone_NoHeadersInjected()
    {
        var collectionAuth = new AuthConfig
        {
            Type = "bearer",
            Token = "should-not-appear"
        };
        var requestAuth = new AuthConfig { Type = "none" };

        var resolved = _handler.ResolveAuth(requestAuth: requestAuth, collectionAuth: collectionAuth);

        // When resolved to null (none), no auth should be applied
        resolved.Should().BeNull();

        // If caller checks for null before applying, no headers are added
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        if (resolved is not null)
        {
            await _handler.ApplyAuthAsync(request, resolved);
        }

        request.Headers.Should().NotContainKey("Authorization");
    }
}
