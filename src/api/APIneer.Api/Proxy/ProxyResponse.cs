namespace APIneer.Api.Proxy;

/// <summary>
/// The result of executing a proxied HTTP request, including timing, headers, body, and any error.
/// </summary>
public class ProxyResponse
{
    /// <summary>
    /// HTTP status code from the target. 0 if the request never reached the target (e.g., timeout, DNS failure).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response headers returned by the target.
    /// </summary>
    public Dictionary<string, IEnumerable<string>> Headers { get; set; } = new();

    /// <summary>
    /// Response body as a string (null for HEAD requests or empty responses).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Wall-clock time in milliseconds from request sent to response fully received.
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Total response size in bytes (headers + body).
    /// </summary>
    public long ResponseSizeBytes { get; set; }

    /// <summary>
    /// Structured error information when the request could not complete (timeout, DNS, connection refused, etc.).
    /// Null on success.
    /// </summary>
    public ProxyError? Error { get; set; }

    /// <summary>
    /// Ordered list of redirect hops when <see cref="ProxyRequest.CaptureRedirectChain"/> is true.
    /// </summary>
    public List<RedirectEntry>? RedirectChain { get; set; }

    /// <summary>
    /// True when the request completed successfully (status code received, no transport-level error).
    /// </summary>
    public bool IsSuccess => Error is null;
}
