using System.Net;
using System.Text.Json;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// Tests that the proxy engine correctly sends requests for every HTTP method
/// and returns accurate status codes, headers, and body content.
/// </summary>
public class ProxySuccessTests : IAsyncLifetime
{
    private readonly TestHttpServer _server = new();
    private IProxyEngine _proxy = null!;

    public async Task InitializeAsync()
    {
        await _server.StartWithDefaults();
        _proxy = CreateProxyEngine();
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task Get_ReturnsCorrectStatusCode()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.IsSuccess.Should().BeTrue();
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task Get_ReturnsResponseHeaders()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.Headers.Should().NotBeEmpty();
        response.Headers.Should().ContainKey("X-Custom-Response");
    }

    [Fact]
    public async Task Get_ReturnsResponseBody()
    {
        var request = new ProxyRequest
        {
            Method = "GET",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.Body.Should().NotBeNullOrEmpty();
        response.Body.Should().Contain("GET echo");
    }

    [Fact]
    public async Task Post_SendsBodyAndReturnsResponse()
    {
        var request = new ProxyRequest
        {
            Method = "POST",
            Url = $"{_server.BaseUrl}/echo",
            Body = """{"name":"test"}""",
            BodyType = "application/json",
            Headers = { ["Content-Type"] = "application/json" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().NotBeNullOrEmpty();
        response.Body.Should().Contain("POST echo");

        // The echo endpoint should return the body we sent
        var json = JsonDocument.Parse(response.Body!);
        json.RootElement.GetProperty("receivedBody").GetString()
            .Should().Contain("test");
    }

    [Fact]
    public async Task Put_WorksCorrectly()
    {
        var request = new ProxyRequest
        {
            Method = "PUT",
            Url = $"{_server.BaseUrl}/echo",
            Body = """{"updated":true}""",
            Headers = { ["Content-Type"] = "application/json" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("PUT echo");
    }

    [Fact]
    public async Task Patch_WorksCorrectly()
    {
        var request = new ProxyRequest
        {
            Method = "PATCH",
            Url = $"{_server.BaseUrl}/echo",
            Body = """{"patched":true}""",
            Headers = { ["Content-Type"] = "application/json" }
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("PATCH echo");
    }

    [Fact]
    public async Task Delete_WorksCorrectly()
    {
        var request = new ProxyRequest
        {
            Method = "DELETE",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("DELETE echo");
    }

    [Fact]
    public async Task Head_ReturnsHeadersOnly()
    {
        var request = new ProxyRequest
        {
            Method = "HEAD",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        // HEAD responses MUST NOT contain a body per HTTP spec
        response.Body.Should().BeNullOrEmpty();
        response.Headers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Options_ReturnsAllowHeader()
    {
        var request = new ProxyRequest
        {
            Method = "OPTIONS",
            Url = $"{_server.BaseUrl}/echo"
        };

        var response = await _proxy.SendAsync(request);

        response.StatusCode.Should().Be(200);
        response.Headers.Should().ContainKey("Allow");
    }

    /// <summary>
    /// Factory for creating the proxy engine under test.
    /// Will be replaced with the real implementation once it exists.
    /// </summary>
    private static IProxyEngine CreateProxyEngine() => new ProxyEngine(new TestHttpClientFactory());
}
