using System.Text.Json;

namespace APIneer.Api.Auth;

public record DeviceCodeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn,
    int Interval);

public record TokenPollResult(
    string Status,       // "pending", "complete", "expired", "denied", "slow_down", "error"
    string? AccessToken,
    string? TokenType,
    string? Scope);

/// <summary>
/// Implements the GitHub OAuth 2.0 Device Authorization Grant (RFC 8628).
/// Used to authenticate APIneer with GitHub for MCP server connections.
/// </summary>
public class GitHubDeviceCodeAuth(IHttpClientFactory httpClientFactory, ILogger<GitHubDeviceCodeAuth> logger)
{
    /// <summary>
    /// Initiates the device flow by requesting a device code and user code from GitHub.
    /// </summary>
    public async Task<DeviceCodeResponse> StartDeviceFlowAsync(string clientId, string? scopes = null)
    {
        var client = httpClientFactory.CreateClient();

        var formParams = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId)
        };
        if (!string.IsNullOrWhiteSpace(scopes))
            formParams.Add(new("scope", scopes));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code");
        request.Headers.Add("Accept", "application/json");
        request.Content = new FormUrlEncodedContent(formParams);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("GitHub device code request failed ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"GitHub device code request failed with status {(int)response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new DeviceCodeResponse(
            DeviceCode: root.GetProperty("device_code").GetString()!,
            UserCode: root.GetProperty("user_code").GetString()!,
            VerificationUri: root.GetProperty("verification_uri").GetString()!,
            ExpiresIn: root.GetProperty("expires_in").GetInt32(),
            Interval: root.GetProperty("interval").GetInt32());
    }

    /// <summary>
    /// Polls GitHub to exchange a device code for an access token.
    /// Call this repeatedly at the interval returned by <see cref="StartDeviceFlowAsync"/>.
    /// </summary>
    public async Task<TokenPollResult> PollForTokenAsync(string clientId, string deviceCode)
    {
        var client = httpClientFactory.CreateClient();

        var formParams = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId),
            new("device_code", deviceCode),
            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        request.Headers.Add("Accept", "application/json");
        request.Content = new FormUrlEncodedContent(formParams);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("GitHub token poll request failed ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"GitHub token poll request failed with status {(int)response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp))
        {
            var error = errorProp.GetString();
            logger.LogDebug("GitHub device flow poll error: {Error}", error);
            return error switch
            {
                "authorization_pending" => new TokenPollResult("pending", null, null, null),
                "slow_down"             => new TokenPollResult("slow_down", null, null, null),
                "expired_token"         => new TokenPollResult("expired", null, null, null),
                "access_denied"         => new TokenPollResult("denied", null, null, null),
                _                       => new TokenPollResult("error", null, null, null)
            };
        }

        return new TokenPollResult(
            Status: "complete",
            AccessToken: root.TryGetProperty("access_token", out var at) ? at.GetString() : null,
            TokenType: root.TryGetProperty("token_type", out var tt) ? tt.GetString() : null,
            Scope: root.TryGetProperty("scope", out var sc) ? sc.GetString() : null);
    }
}
