using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.Environments;

/// <summary>
/// TDD Red-phase tests for secret variable handling.
/// Security invariants from docs/security-architecture.md:
/// - Secret values are masked in API responses (***masked***)
/// - Secret values ARE resolved at send time (proxy gets real value)
/// - Raw secret values can never be retrieved via API
/// - Secret flag persists across updates
/// All tests MUST FAIL until implementation is built.
/// </summary>
public class SecretVariableTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // Secret Masking in GET Response
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_MaskSecretValue_InGetResponse()
    {
        var (_, envId, _) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "api_key", value: "super-secret-key-12345", isSecret: true);

        var response = await _client.GetAsync($"/api/environments/{envId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        var secretVar = env!.Variables!.First(v => v.Key == "api_key");

        // Value must be masked — security invariant
        secretVar.Value.Should().Be("***masked***");
        secretVar.IsSecret.Should().BeTrue();
    }

    [Fact]
    public async Task Should_NotMaskNonSecretValue_InGetResponse()
    {
        var (_, envId, _) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "base_url", value: "https://api.example.com", isSecret: false);

        var response = await _client.GetAsync($"/api/environments/{envId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        var plainVar = env!.Variables!.First(v => v.Key == "base_url");

        // Non-secret values are visible
        plainVar.Value.Should().Be("https://api.example.com");
        plainVar.IsSecret.Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // Secret Resolved at Send Time
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveSecretValue_AtSendTime()
    {
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        // Activate environment
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        // Add secret variable
        await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("token", "real-secret-token", isSecret: true)));

        // Resolve should use the REAL value (not masked) for proxy execution
        var payload = new { url = "https://api.example.com", headers = "{\"Authorization\": \"Bearer {{token}}\"}" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        // The resolved value should contain the real secret (for proxy use)
        body.Should().Contain("real-secret-token");
        body.Should().NotContain("***masked***");
    }

    // ──────────────────────────────────────────────
    // Cannot Retrieve Raw Secret via API
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_NeverReturnRawSecretValue_ViaGetEndpoint()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "password", value: "p@ssw0rd-super-secret", isSecret: true);

        // Try direct variable endpoint
        var response = await _client.GetAsync($"/api/environments/{envId}/variables/{varId}");

        // Should either mask the value or not expose individual variable GET
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("p@ssw0rd-super-secret",
                "raw secret value must NEVER appear in API responses (security invariant)");
        }

        // Also check full environment response
        var envResponse = await _client.GetAsync($"/api/environments/{envId}");
        var envBody = await envResponse.Content.ReadAsStringAsync();
        envBody.Should().NotContain("p@ssw0rd-super-secret",
            "raw secret value must NEVER appear in environment GET response");
    }

    // ──────────────────────────────────────────────
    // Secret Flag Persists on Update
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PersistSecretFlag_WhenVariableUpdated()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "api_key", value: "original-secret", isSecret: true);

        // Update the variable value but keep it secret
        var response = await _client.PutAsync(
            $"/api/environments/{envId}/variables/{varId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateVariablePayload("api_key", "new-secret-value", isSecret: true)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-fetch and verify it's still secret and masked
        var getResponse = await _client.GetAsync($"/api/environments/{envId}");
        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getResponse);
        var variable = env!.Variables!.First(v => v.Key == "api_key");

        variable.IsSecret.Should().BeTrue();
        variable.Value.Should().Be("***masked***");
    }

    [Fact]
    public async Task Should_AllowChangingSecretFlagToFalse()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "api_key", value: "was-secret", isSecret: true);

        // Update to no longer be secret
        var response = await _client.PutAsync(
            $"/api/environments/{envId}/variables/{varId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateVariablePayload("api_key", "no-longer-secret", isSecret: false)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-fetch — value should now be visible
        var getResponse = await _client.GetAsync($"/api/environments/{envId}");
        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getResponse);
        var variable = env!.Variables!.First(v => v.Key == "api_key");

        variable.IsSecret.Should().BeFalse();
        variable.Value.Should().Be("no-longer-secret");
    }
}
