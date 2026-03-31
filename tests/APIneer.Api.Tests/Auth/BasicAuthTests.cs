using System.Text;
using APIneer.Api.Auth;
using APIneer.Api.Proxy;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// RED-phase tests for Basic authentication.
/// Auth type "basic" with username + password → adds "Authorization: Basic {base64(user:pass)}" header.
/// All tests should FAIL until the AuthHandler is implemented.
/// </summary>
public class BasicAuthTests
{
    private readonly AuthHandler _handler = new(new HttpClient());

    [Fact]
    public async Task Basic_AddsAuthorizationHeader()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = "password123"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers.Should().ContainKey("Authorization");
    }

    [Fact]
    public async Task Basic_HeaderStartsWithBasicPrefix()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = "password123"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Headers["Authorization"].Should().StartWith("Basic ");
    }

    [Fact]
    public async Task Basic_HeaderContainsCorrectBase64Encoding()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = "password123"
        };

        await _handler.ApplyAuthAsync(request, auth);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:password123"));
        request.Headers["Authorization"].Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Basic_SpecialCharactersInPassword_EncodesCorrectly()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "user@domain.com",
            Password = "p@$$w0rd!#%"
        };

        await _handler.ApplyAuthAsync(request, auth);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("user@domain.com:p@$$w0rd!#%"));
        request.Headers["Authorization"].Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Basic_ColonInPassword_EncodesCorrectly()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = "pass:word"
        };

        await _handler.ApplyAuthAsync(request, auth);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:pass:word"));
        request.Headers["Authorization"].Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Basic_EmptyUsername_StillEncodes()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "",
            Password = "password123"
        };

        await _handler.ApplyAuthAsync(request, auth);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(":password123"));
        request.Headers["Authorization"].Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Basic_EmptyPassword_StillEncodes()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = ""
        };

        await _handler.ApplyAuthAsync(request, auth);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:"));
        request.Headers["Authorization"].Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Basic_DoesNotModifyUrl()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = "password123"
        };

        await _handler.ApplyAuthAsync(request, auth);

        request.Url.Should().Be("https://api.example.com/data");
    }

    [Fact]
    public async Task Basic_NullUsername_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = null,
            Password = "password123"
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Basic_NullPassword_ThrowsArgumentException()
    {
        var request = new ProxyRequest { Method = "GET", Url = "https://api.example.com/data" };
        var auth = new AuthConfig
        {
            Type = "basic",
            Username = "admin",
            Password = null
        };

        var act = () => _handler.ApplyAuthAsync(request, auth);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
