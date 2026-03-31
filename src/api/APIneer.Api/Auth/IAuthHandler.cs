using APIneer.Api.Proxy;

namespace APIneer.Api.Auth;

/// <summary>
/// Applies authentication credentials to an outgoing proxy request and
/// resolves auth inheritance between collection and request levels.
/// </summary>
public interface IAuthHandler
{
    /// <summary>
    /// Modify <paramref name="request"/> in place to inject the credentials
    /// described by <paramref name="authConfig"/> (headers, query params, etc.).
    /// </summary>
    Task ApplyAuthAsync(ProxyRequest request, AuthConfig authConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve which auth config to use given request-level and collection-level configs.
    /// Request auth overrides collection; "none" explicitly disables inherited auth.
    /// </summary>
    AuthConfig? ResolveAuth(AuthConfig? requestAuth, AuthConfig? collectionAuth);
}
