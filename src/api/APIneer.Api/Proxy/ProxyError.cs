namespace APIneer.Api.Proxy;

/// <summary>
/// A structured error describing why a proxied request could not complete.
/// Returned instead of throwing exceptions so the frontend always gets a renderable result.
/// Known codes: TIMEOUT, CONNECTION_REFUSED, DNS_FAILURE, INVALID_URL, REQUEST_ERROR.
/// </summary>
public record ProxyError(string Code, string Message);
