namespace APIneer.Api.Proxy;

/// <summary>
/// Describes an HTTP request to be sent by the proxy engine on behalf of the user.
/// </summary>
public class ProxyRequest
{
    public required string Method { get; set; }
    public required string Url { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public string? BodyType { get; set; }

    /// <summary>
    /// Request timeout in seconds. Defaults to 30.
    /// Valid range: 1–300.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Whether to follow HTTP redirects. Defaults to true.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// When true, the proxy records each redirect hop in <see cref="ProxyResponse.RedirectChain"/>.
    /// </summary>
    public bool CaptureRedirectChain { get; set; }
}
