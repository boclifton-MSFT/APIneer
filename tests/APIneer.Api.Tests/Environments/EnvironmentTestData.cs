using System.Text.Json;
using System.Text.Json.Serialization;

namespace APIneer.Api.Tests.Environments;

/// <summary>
/// Shared test data and helpers for Environment & Variable API tests.
/// Defines the expected API contract for Environments endpoints.
/// </summary>
public static class EnvironmentTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Seed a workspace via the API so environments have valid FKs.
    /// </summary>
    public static async Task<Guid> SeedWorkspaceAsync(HttpClient client)
    {
        var payload = new { name = "Test Workspace" };
        var response = await client.PostAsync("/api/workspaces",
            JsonContent(payload));

        if (response.IsSuccessStatusCode)
        {
            var body = await Deserialize<IdResponse>(response);
            return body!.Id;
        }

        return WellKnownWorkspaceId;
    }

    /// <summary>
    /// Seed a workspace + environment and return both IDs.
    /// </summary>
    public static async Task<(Guid WorkspaceId, Guid EnvironmentId)> SeedEnvironmentAsync(
        HttpClient client, string envName = "Development")
    {
        var workspaceId = await SeedWorkspaceAsync(client);

        var payload = new { name = envName, workspaceId };
        var response = await client.PostAsync("/api/environments",
            JsonContent(payload));

        if (response.IsSuccessStatusCode)
        {
            var body = await Deserialize<EnvironmentResponse>(response);
            return (workspaceId, body!.Id);
        }

        return (workspaceId, Guid.Empty);
    }

    /// <summary>
    /// Seed an environment with a variable already added.
    /// </summary>
    public static async Task<(Guid WorkspaceId, Guid EnvironmentId, Guid VariableId)>
        SeedEnvironmentWithVariableAsync(
            HttpClient client,
            string key = "api_key",
            string value = "test-value-123",
            bool isSecret = false)
    {
        var (workspaceId, envId) = await SeedEnvironmentAsync(client);

        var varPayload = new { key, value, isSecret };
        var varResponse = await client.PostAsync(
            $"/api/environments/{envId}/variables",
            JsonContent(varPayload));

        if (varResponse.IsSuccessStatusCode)
        {
            var varBody = await Deserialize<VariableResponse>(varResponse);
            return (workspaceId, envId, varBody!.Id);
        }

        return (workspaceId, envId, Guid.Empty);
    }

    public static readonly Guid WellKnownWorkspaceId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // --- Payloads ---

    public static object CreateEnvironmentPayload(Guid workspaceId, string name = "Development") =>
        new { name, workspaceId };

    public static object UpdateEnvironmentPayload(string name = "Staging") =>
        new { name };

    public static object CreateVariablePayload(
        string key = "api_key", string value = "test-value-123", bool isSecret = false) =>
        new { key, value, isSecret };

    public static object UpdateVariablePayload(
        string key = "updated_key", string value = "updated-value", bool isSecret = false) =>
        new { key, value, isSecret };

    // --- Serialization helpers ---

    public static StringContent JsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    public static async Task<T?> Deserialize<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    // --- Response DTOs ---

    public record IdResponse(Guid Id);

    public record EnvironmentResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        VariableResponse[]? Variables);

    public record VariableResponse(
        Guid Id,
        Guid EnvironmentId,
        string Key,
        string Value,
        bool IsSecret,
        DateTime CreatedAt);

    /// <summary>
    /// Response shape when variable resolution is applied (e.g., send-time).
    /// </summary>
    public record ResolvedRequestResponse(
        string Url,
        string? Headers,
        string? Body);
}
