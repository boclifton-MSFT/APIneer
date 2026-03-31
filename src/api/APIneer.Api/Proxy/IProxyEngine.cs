namespace APIneer.Api.Proxy;

/// <summary>
/// Core proxy engine that sends HTTP requests on behalf of the user and captures responses.
/// This is the heart of APIneer's API testing capability.
/// </summary>
public interface IProxyEngine
{
    /// <summary>
    /// Execute an HTTP request against the target URL and return a structured response.
    /// Never throws for transport-level errors; instead returns a <see cref="ProxyResponse"/> with
    /// <see cref="ProxyResponse.Error"/> populated.
    /// </summary>
    Task<ProxyResponse> SendAsync(ProxyRequest request, CancellationToken cancellationToken = default);
}
