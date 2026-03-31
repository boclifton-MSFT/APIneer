using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.Environments;

/// <summary>
/// TDD Red-phase tests for active environment management.
/// Only one environment can be active per workspace at a time.
/// Variable resolution uses the active environment.
/// All tests MUST FAIL until implementation is built.
/// </summary>
public class ActiveEnvironmentTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // PUT /api/environments/{id}/activate
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ActivateEnvironment_When_ActivateEndpointCalled()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.PutAsync($"/api/environments/{envId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's now active
        var getResponse = await _client.GetAsync($"/api/environments/{envId}");
        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getResponse);
        env!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ActivatingNonExistentEnvironment()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PutAsync($"/api/environments/{nonExistentId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // Only One Active Per Workspace
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_DeactivatePrevious_When_NewEnvironmentActivated()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        // Create two environments in the same workspace
        var env1Response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Development")));
        var env1 = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(env1Response);

        var env2Response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Staging")));
        var env2 = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(env2Response);

        // Activate first
        await _client.PutAsync($"/api/environments/{env1!.Id}/activate", null);

        // Activate second — first should become inactive
        await _client.PutAsync($"/api/environments/{env2!.Id}/activate", null);

        // Verify: env1 is now inactive
        var getEnv1 = await _client.GetAsync($"/api/environments/{env1.Id}");
        var env1Updated = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getEnv1);
        env1Updated!.IsActive.Should().BeFalse();

        // Verify: env2 is active
        var getEnv2 = await _client.GetAsync($"/api/environments/{env2.Id}");
        var env2Updated = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getEnv2);
        env2Updated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Should_AllowOnlyOneActivePerWorkspace_WithManyEnvironments()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        // Create three environments
        var ids = new List<Guid>();
        foreach (var name in new[] { "Dev", "Staging", "Production" })
        {
            var res = await _client.PostAsync("/api/environments",
                EnvironmentTestData.JsonContent(
                    EnvironmentTestData.CreateEnvironmentPayload(workspaceId, name)));
            var created = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(res);
            ids.Add(created!.Id);
        }

        // Activate the last one
        await _client.PutAsync($"/api/environments/{ids[2]}/activate", null);

        // List all and check exactly one is active
        var listResponse = await _client.GetAsync("/api/environments");
        var all = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse[]>(listResponse);

        var workspaceEnvs = all!.Where(e => e.WorkspaceId == workspaceId).ToArray();
        workspaceEnvs.Count(e => e.IsActive).Should().Be(1);
        workspaceEnvs.Single(e => e.IsActive).Id.Should().Be(ids[2]);
    }

    // ──────────────────────────────────────────────
    // Variable Resolution Uses Active Environment
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveFromActiveEnvironment_When_MultipleExist()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        // Create two environments with different values for the same key
        var env1Response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Development")));
        var env1 = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(env1Response);

        var env2Response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Production")));
        var env2 = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(env2Response);

        // Add same key with different values
        await _client.PostAsync($"/api/environments/{env1!.Id}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("base_url", "http://localhost:3000")));

        await _client.PostAsync($"/api/environments/{env2!.Id}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("base_url", "https://prod.example.com")));

        // Activate Production
        await _client.PutAsync($"/api/environments/{env2.Id}/activate", null);

        // Resolve should use Production's value
        var payload = new { url = "{{base_url}}/api/users" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Be("https://prod.example.com/api/users");
    }

    // ──────────────────────────────────────────────
    // No Active Environment → No Resolution
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_NotResolveVariables_When_NoEnvironmentIsActive()
    {
        // Seed workspace but don't activate any environment
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        // Create environment with variable but don't activate
        var envResponse = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId)));
        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(envResponse);

        await _client.PostAsync($"/api/environments/{env!.Id}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("base_url", "https://api.example.com")));

        // Resolve without activating — variables should stay as-is
        var payload = new { url = "{{base_url}}/users" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Be("{{base_url}}/users");
    }

    [Fact]
    public async Task Should_StopResolving_WhenActiveEnvironmentIsDeactivated()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        // Add variable and activate
        await _client.PostAsync($"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("api_key", "my-key")));
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        // Deactivate by activating nothing (or a toggle endpoint)
        await _client.PutAsync($"/api/environments/{envId}/deactivate", null);

        // Resolve should no longer work
        var payload = new { url = "{{api_key}}" };
        var response = await _client.PostAsync("/api/environments/resolve",
            EnvironmentTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await EnvironmentTestData.Deserialize<EnvironmentTestData.ResolvedRequestResponse>(response);
        resolved!.Url.Should().Be("{{api_key}}");
    }
}
