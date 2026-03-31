using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.Environments;

/// <summary>
/// TDD Red-phase tests for {{variable}} resolution in requests.
/// Variables from the active environment should be resolved in URL, headers, and body.
/// All tests MUST FAIL until resolution logic is built.
/// </summary>
public class VariableResolutionTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    /// <summary>
    /// Helper: create workspace → environment → activate → add variables → return envId.
    /// </summary>
    private async Task<Guid> SeedActiveEnvironmentWithVariables(
        Dictionary<string, string> variables)
    {
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        // Activate environment
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        // Add variables
        foreach (var (key, value) in variables)
        {
            await _client.PostAsync(
                $"/api/environments/{envId}/variables",
                EnvironmentTestData.JsonContent(
                    EnvironmentTestData.CreateVariablePayload(key, value)));
        }

        return envId;
    }

    // ──────────────────────────────────────────────
    // URL Resolution
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveVariable_InUrl()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["base_url"] = "https://api.example.com"
        });

        // POST to resolve endpoint — takes a template and returns resolved values
        var payload = new { url = "{{base_url}}/users" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved.Should().NotBeNull();
        resolved!.Url.Should().Be("https://api.example.com/users");
    }

    // ──────────────────────────────────────────────
    // Header Resolution
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveVariable_InHeaders()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["auth_token"] = "Bearer my-secret-token"
        });

        var payload = new
        {
            url = "https://api.example.com",
            headers = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Authorization"] = "{{auth_token}}"
            })
        };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(resolved!.Headers!);
        headers!["Authorization"].Should().Be("Bearer my-secret-token");
    }

    // ──────────────────────────────────────────────
    // Body Resolution
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveVariable_InBody()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["username"] = "testuser"
        });

        var payload = new
        {
            url = "https://api.example.com",
            body = """{"user": "{{username}}"}"""
        };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Body.Should().Contain("testuser");
        resolved.Body.Should().NotContain("{{username}}");
    }

    // ──────────────────────────────────────────────
    // Nested Variables
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveMultipleVariables_InSameString()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["base_url"] = "https://api.example.com",
            ["version"] = "v2"
        });

        var payload = new { url = "{{base_url}}/{{version}}/users" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Be("https://api.example.com/v2/users");
    }

    // ──────────────────────────────────────────────
    // Undefined Variables
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_LeaveUndefinedVariable_AsIs()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["base_url"] = "https://api.example.com"
        });

        var payload = new { url = "{{base_url}}/{{undefined_var}}" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Contain("{{undefined_var}}");
        resolved.Url.Should().StartWith("https://api.example.com/");
    }

    // ──────────────────────────────────────────────
    // Escaped Braces
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_NotResolve_EscapedBraces()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["literal"] = "should-not-appear"
        });

        var payload = new { url = @"https://api.example.com/\{\{literal\}\}" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().NotContain("should-not-appear");
        resolved.Url.Should().Contain("{{literal}}");
    }

    // ──────────────────────────────────────────────
    // Empty Variable Value
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveToEmptyString_When_VariableValueIsEmpty()
    {
        await SeedActiveEnvironmentWithVariables(new()
        {
            ["empty_var"] = ""
        });

        var payload = new { url = "https://api.example.com/{{empty_var}}/users" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Be("https://api.example.com//users");
        resolved.Url.Should().NotContain("{{empty_var}}");
    }
}
