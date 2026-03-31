using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.Environments;

/// <summary>
/// TDD Red-phase tests for Environment and Variable CRUD endpoints.
/// All tests MUST FAIL until endpoints are built.
/// </summary>
public class EnvironmentCrudTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/environments — Create Environment
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedEnvironment_When_ValidPayloadProvided()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        var response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Development");
        created.WorkspaceId.Should().Be(workspaceId);
        created.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Should_SetTimestamps_When_EnvironmentCreated()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        created!.CreatedAt.Should().BeAfter(before);
        created.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Should_ReturnLocationHeader_When_EnvironmentCreated()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        var response = await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/environments/");
    }

    // ──────────────────────────────────────────────
    // GET /api/environments — List All
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoEnvironmentsExist()
    {
        var response = await _client.GetAsync("/api/environments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var environments = JsonSerializer.Deserialize<EnvironmentTestData.EnvironmentResponse[]>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        environments.Should().NotBeNull();
        environments.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnAllEnvironments_When_MultipleExist()
    {
        var workspaceId = await EnvironmentTestData.SeedWorkspaceAsync(_client);

        await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Development")));
        await _client.PostAsync("/api/environments",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateEnvironmentPayload(workspaceId, "Staging")));

        var response = await _client.GetAsync("/api/environments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var environments = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse[]>(response);
        environments.Should().NotBeNull();
        environments!.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    // ──────────────────────────────────────────────
    // GET /api/environments/{id} — Get with Variables
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnEnvironment_When_ValidIdProvided()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.GetAsync($"/api/environments/{envId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(envId);
        fetched.Name.Should().Be("Development");
    }

    [Fact]
    public async Task Should_IncludeVariables_When_GettingEnvironmentById()
    {
        var (_, envId, _) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(_client);

        var response = await _client.GetAsync($"/api/environments/{envId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        fetched!.Variables.Should().NotBeNull();
        fetched.Variables!.Length.Should().BeGreaterThanOrEqualTo(1);
        fetched.Variables[0].Key.Should().Be("api_key");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_EnvironmentIdDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/environments/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // PUT /api/environments/{id} — Update
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnUpdatedEnvironment_When_ValidUpdateProvided()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.PutAsync($"/api/environments/{envId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateEnvironmentPayload("Production")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Production");
    }

    [Fact]
    public async Task Should_UpdateTimestamp_When_EnvironmentUpdated()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        await Task.Delay(100);

        var response = await _client.PutAsync($"/api/environments/{envId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateEnvironmentPayload("Updated")));

        var updated = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(response);
        updated!.UpdatedAt.Should().BeAfter(updated.CreatedAt);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingNonExistentEnvironment()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PutAsync($"/api/environments/{nonExistentId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateEnvironmentPayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/environments/{id} — Delete (cascades variables)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnNoContent_When_EnvironmentDeleted()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.DeleteAsync($"/api/environments/{envId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingDeletedEnvironment()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        await _client.DeleteAsync($"/api/environments/{envId}");

        var getResponse = await _client.GetAsync($"/api/environments/{envId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_CascadeDeleteVariables_When_EnvironmentDeleted()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(_client);

        await _client.DeleteAsync($"/api/environments/{envId}");

        // The variable should no longer be accessible
        var varResponse = await _client.GetAsync($"/api/environments/{envId}/variables/{varId}");
        varResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingNonExistentEnvironment()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/environments/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // POST /api/environments/{id}/variables — Add Variable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedVariable_When_ValidPayloadProvided()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("base_url", "https://api.example.com")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await EnvironmentTestData.Deserialize<EnvironmentTestData.VariableResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Key.Should().Be("base_url");
        created.Value.Should().Be("https://api.example.com");
        created.IsSecret.Should().BeFalse();
        created.EnvironmentId.Should().Be(envId);
    }

    [Fact]
    public async Task Should_CreateSecretVariable_When_IsSecretFlagSet()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        var response = await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("token", "super-secret", isSecret: true)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await EnvironmentTestData.Deserialize<EnvironmentTestData.VariableResponse>(response);
        created!.IsSecret.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_AddingVariableToNonExistentEnvironment()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/environments/{nonExistentId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // PUT /api/environments/{id}/variables/{varId} — Update Variable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnUpdatedVariable_When_ValidUpdateProvided()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(_client);

        var response = await _client.PutAsync(
            $"/api/environments/{envId}/variables/{varId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateVariablePayload("new_key", "new-value")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await EnvironmentTestData.Deserialize<EnvironmentTestData.VariableResponse>(response);
        updated.Should().NotBeNull();
        updated!.Key.Should().Be("new_key");
        updated.Value.Should().Be("new-value");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingNonExistentVariable()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);
        var nonExistentVarId = Guid.NewGuid();

        var response = await _client.PutAsync(
            $"/api/environments/{envId}/variables/{nonExistentVarId}",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.UpdateVariablePayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/environments/{id}/variables/{varId} — Remove Variable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnNoContent_When_VariableDeleted()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(_client);

        var response = await _client.DeleteAsync(
            $"/api/environments/{envId}/variables/{varId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingNonExistentVariable()
    {
        var (_, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);
        var nonExistentVarId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"/api/environments/{envId}/variables/{nonExistentVarId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_RemoveVariableFromEnvironment_When_Deleted()
    {
        var (_, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(_client);

        await _client.DeleteAsync($"/api/environments/{envId}/variables/{varId}");

        // Re-fetch environment — variables list should be empty
        var getResponse = await _client.GetAsync($"/api/environments/{envId}");
        var env = await EnvironmentTestData.Deserialize<EnvironmentTestData.EnvironmentResponse>(getResponse);
        env!.Variables.Should().BeEmpty();
    }
}
