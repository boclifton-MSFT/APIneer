using System.Net;
using System.Text;
using System.Text.Json;
using APIneer.Api.Proxy;

namespace APIneer.Api.Auth;

/// <summary>
/// Applies authentication credentials to outgoing proxy requests and
/// resolves auth inheritance between collection and request levels.
/// Supports: api_key (header/query), bearer, basic, oauth2 (client credentials).
/// </summary>
public class AuthHandler : IAuthHandler
{
    private readonly HttpClient _httpClient;

    public AuthHandler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task ApplyAuthAsync(ProxyRequest request, AuthConfig authConfig, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authConfig);

        switch (authConfig.Type?.ToLowerInvariant())
        {
            case "api_key":
                ApplyApiKey(request, authConfig);
                break;

            case "bearer":
                ApplyBearer(request, authConfig);
                break;

            case "basic":
                ApplyBasic(request, authConfig);
                break;

            case "oauth2":
                await ApplyOAuth2Async(request, authConfig, cancellationToken);
                break;

            case "none":
                break;

            default:
                throw new ArgumentException($"Unsupported auth type: {authConfig.Type}", nameof(authConfig));
        }
    }

    /// <inheritdoc />
    public AuthConfig? ResolveAuth(AuthConfig? requestAuth, AuthConfig? collectionAuth)
    {
        // Request explicitly set to "none" → disable all auth
        if (requestAuth is not null &&
            string.Equals(requestAuth.Type, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Request has its own auth → use it (overrides collection)
        if (requestAuth is not null)
        {
            return requestAuth;
        }

        // Fall back to collection auth (may also be null)
        return collectionAuth;
    }

    // ── API Key ─────────────────────────────────────────────────

    private static void ApplyApiKey(ProxyRequest request, AuthConfig auth)
    {
        if (string.IsNullOrEmpty(auth.KeyName))
            throw new ArgumentException("API Key auth requires a key name.", nameof(auth));

        if (auth.KeyValue is null)
            throw new ArgumentException("API Key auth requires a key value.", nameof(auth));

        var placement = auth.Placement?.ToLowerInvariant() ?? "header";

        if (placement == "query")
        {
            var encodedName = Uri.EscapeDataString(auth.KeyName);
            var encodedValue = Uri.EscapeDataString(auth.KeyValue);
            var separator = request.Url.Contains('?') ? "&" : "?";
            request.Url = $"{request.Url}{separator}{encodedName}={encodedValue}";
        }
        else
        {
            request.Headers[auth.KeyName] = auth.KeyValue;
        }
    }

    // ── Bearer Token ────────────────────────────────────────────

    private static void ApplyBearer(ProxyRequest request, AuthConfig auth)
    {
        if (string.IsNullOrEmpty(auth.Token))
            throw new ArgumentException("Bearer auth requires a token.", nameof(auth));

        request.Headers["Authorization"] = $"Bearer {auth.Token}";
    }

    // ── Basic Auth ──────────────────────────────────────────────

    private static void ApplyBasic(ProxyRequest request, AuthConfig auth)
    {
        if (auth.Username is null)
            throw new ArgumentException("Basic auth requires a username.", nameof(auth));

        if (auth.Password is null)
            throw new ArgumentException("Basic auth requires a password.", nameof(auth));

        var credentials = $"{auth.Username}:{auth.Password}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        request.Headers["Authorization"] = $"Basic {encoded}";
    }

    // ── OAuth 2.0 Client Credentials ────────────────────────────

    private async Task ApplyOAuth2Async(ProxyRequest request, AuthConfig auth, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(auth.TokenEndpoint))
            throw new ArgumentException("OAuth2 auth requires a token endpoint.", nameof(auth));

        if (string.IsNullOrEmpty(auth.ClientId))
            throw new ArgumentException("OAuth2 auth requires a client ID.", nameof(auth));

        if (string.IsNullOrEmpty(auth.ClientSecret))
            throw new ArgumentException("OAuth2 auth requires a client secret.", nameof(auth));

        // Use cached token if still valid
        if (!string.IsNullOrEmpty(auth.AccessToken) &&
            auth.TokenExpiresAt.HasValue &&
            auth.TokenExpiresAt.Value > DateTime.UtcNow)
        {
            request.Headers["Authorization"] = $"Bearer {auth.AccessToken}";
            return;
        }

        // Fetch new token from endpoint
        var formParams = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", auth.ClientId),
            new("client_secret", auth.ClientSecret)
        };

        if (!string.IsNullOrEmpty(auth.Scope))
        {
            formParams.Add(new("scope", auth.Scope));
        }

        using var content = new FormUrlEncodedContent(formParams);
        using var response = await _httpClient.PostAsync(auth.TokenEndpoint, content, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OAuth2 token request failed with status {(int)response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("OAuth2 token response missing access_token.");

        auth.AccessToken = accessToken;

        if (root.TryGetProperty("expires_in", out var expiresIn))
        {
            auth.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn.GetInt32());
        }

        request.Headers["Authorization"] = $"Bearer {accessToken}";
    }
}
