using System.Text.Json;
using System.Text.Json.Serialization;
using APIneer.Api.Tests.Requests;

namespace APIneer.Api.Tests.Collections;

/// <summary>
/// Shared test data and helpers for Collection & Folder API tests.
/// Defines the expected API contract for collections, folders, ordering, and duplication.
/// </summary>
public static class CollectionTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ──────────────────────────────────────────────
    // Seed helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Seed a workspace via the API (or fall back to well-known ID).
    /// Returns the workspaceId.
    /// </summary>
    public static async Task<Guid> SeedWorkspaceAsync(HttpClient client)
    {
        var payload = new { name = "Test Workspace" };
        var response = await client.PostAsync("/api/workspaces",
            TestData.JsonContent(payload));

        if (response.IsSuccessStatusCode)
        {
            var body = await Deserialize<IdResponse>(response);
            return body!.Id;
        }

        return TestData.WellKnownWorkspaceId;
    }

    /// <summary>
    /// Seed a workspace + collection. Returns the collectionId.
    /// </summary>
    public static async Task<Guid> SeedCollectionAsync(HttpClient client)
    {
        var workspaceId = await SeedWorkspaceAsync(client);
        var payload = new { name = "Seeded Collection", description = "Auto-created for tests", workspaceId };
        var response = await client.PostAsync("/api/collections",
            TestData.JsonContent(payload));

        if (response.IsSuccessStatusCode)
        {
            var body = await Deserialize<IdResponse>(response);
            return body!.Id;
        }

        return TestData.WellKnownCollectionId;
    }

    /// <summary>
    /// Create a folder inside a collection. Returns the folderId.
    /// </summary>
    public static async Task<Guid> CreateFolderAsync(
        HttpClient client, Guid collectionId, string name, Guid? parentFolderId = null)
    {
        var payload = parentFolderId.HasValue
            ? (object)new { name, parentFolderId }
            : new { name };

        var response = await client.PostAsync(
            $"/api/collections/{collectionId}/folders",
            TestData.JsonContent(payload));

        var body = await Deserialize<IdResponse>(response);
        return body!.Id;
    }

    /// <summary>
    /// Create a request inside a collection (optionally in a folder). Returns the requestId.
    /// </summary>
    public static async Task<Guid> CreateRequestAsync(
        HttpClient client, Guid collectionId, string name, Guid? folderId = null)
    {
        var payload = new
        {
            name,
            method = "GET",
            url = "https://api.example.com/test",
            collectionId,
            folderId
        };

        var response = await client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        var body = await Deserialize<IdResponse>(response);
        return body!.Id;
    }

    // ──────────────────────────────────────────────
    // Payloads
    // ──────────────────────────────────────────────

    public static object CreateCollectionPayload(Guid workspaceId, string name = "My Collection",
        string? description = null)
        => new { name, description, workspaceId };

    public static object UpdateCollectionPayload(string name = "Updated Collection",
        string? description = "Updated description")
        => new { name, description };

    // ──────────────────────────────────────────────
    // Response DTOs
    // ──────────────────────────────────────────────

    public record IdResponse(Guid Id);

    public record CollectionResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        FolderResponse[]? Folders,
        RequestSummaryResponse[]? Requests);

    public record FolderResponse(
        Guid Id,
        Guid CollectionId,
        Guid? ParentFolderId,
        string Name,
        int SortOrder,
        FolderResponse[]? SubFolders,
        RequestSummaryResponse[]? Requests);

    public record RequestSummaryResponse(
        Guid Id,
        Guid CollectionId,
        Guid? FolderId,
        string Name,
        string Method,
        string Url,
        int SortOrder);

    public record ReorderPayload(Guid[] ItemIds);

    // ──────────────────────────────────────────────
    // Serialization helpers
    // ──────────────────────────────────────────────

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
}
