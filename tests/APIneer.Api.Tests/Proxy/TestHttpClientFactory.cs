using System.Net;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Simple IHttpClientFactory for proxy tests that creates HttpClients with redirect disabled,
/// matching the production SocketsHttpHandler pooling configuration.
/// </summary>
internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new HttpClient(handler);
    }
}
