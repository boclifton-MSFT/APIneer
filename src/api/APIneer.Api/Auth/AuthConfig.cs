namespace APIneer.Api.Auth;

/// <summary>
/// Describes the authentication configuration for a request or collection.
/// The <see cref="Type"/> discriminator determines which fields are relevant.
/// </summary>
public class AuthConfig
{
    /// <summary>
    /// Auth type: "none", "api_key", "bearer", "basic", "oauth2".
    /// </summary>
    public required string Type { get; set; }

    // ── API Key ──
    public string? KeyName { get; set; }
    public string? KeyValue { get; set; }

    /// <summary>"header" or "query"</summary>
    public string? Placement { get; set; }

    // ── Bearer Token ──
    public string? Token { get; set; }

    // ── Basic Auth ──
    public string? Username { get; set; }
    public string? Password { get; set; }

    // ── OAuth 2.0 (Client Credentials) ──
    public string? TokenEndpoint { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}
