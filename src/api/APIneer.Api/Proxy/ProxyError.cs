namespace APIneer.Api.Proxy;

/// <summary>
/// A structured error describing why a proxied request could not complete.
/// Returned instead of throwing exceptions so the frontend always gets a renderable result.
/// </summary>
public class ProxyError
{
    /// <summary>
    /// Machine-readable error code.
    /// Known values: TIMEOUT, CONNECTION_REFUSED, DNS_FAILURE, INVALID_URL, REQUEST_ERROR.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; set; }
}
