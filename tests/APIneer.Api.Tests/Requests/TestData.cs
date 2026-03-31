using System.Text.Json;
using System.Text.Json.Serialization;

namespace APIneer.Api.Tests.Requests;

/// <summary>
/// Shared test data and helpers for Request API tests.
/// Defines the expected API contract that Marcus will implement.
/// </summary>
public static class TestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Seed a workspace + collection via the API so requests have valid FKs.
    /// Returns the collectionId to use when creating requests.
    /// </summary>
    public static async Task<Guid> SeedCollectionAsync(HttpClient client)
    {
        // Create a workspace first
        var workspacePayload = new { name = "Test Workspace" };
        var wsResponse = await client.PostAsync("/api/workspaces",
            JsonContent(workspacePayload));

        // If workspace endpoint doesn't exist yet, we still want the test to
        // exercise the request endpoints. Use a well-known GUID as fallback.
        Guid workspaceId;
        if (wsResponse.IsSuccessStatusCode)
        {
            var wsBody = await Deserialize<IdResponse>(wsResponse);
            workspaceId = wsBody!.Id;
        }
        else
        {
            workspaceId = WellKnownWorkspaceId;
        }

        // Create a collection
        var collectionPayload = new { name = "Test Collection", workspaceId };
        var colResponse = await client.PostAsync("/api/collections",
            JsonContent(collectionPayload));

        if (colResponse.IsSuccessStatusCode)
        {
            var colBody = await Deserialize<IdResponse>(colResponse);
            return colBody!.Id;
        }

        return WellKnownCollectionId;
    }

    // Well-known IDs for seeding directly into the DB when endpoints aren't ready
    public static readonly Guid WellKnownWorkspaceId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid WellKnownCollectionId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>A minimal valid request payload.</summary>
    public static object ValidGetRequest(Guid collectionId) => new
    {
        name = "Get Users",
        method = "GET",
        url = "https://api.example.com/users",
        collectionId
    };

    /// <summary>A POST request with headers and body.</summary>
    public static object ValidPostRequest(Guid collectionId) => new
    {
        name = "Create User",
        method = "POST",
        url = "https://api.example.com/users",
        headers = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["X-Custom-Header"] = "test-value"
        }),
        body = """{"username":"testuser","email":"test@example.com"}""",
        bodyType = "application/json",
        collectionId
    };

    /// <summary>A PUT request for update testing.</summary>
    public static object ValidPutRequest(Guid collectionId) => new
    {
        name = "Update User",
        method = "PUT",
        url = "https://api.example.com/users/1",
        headers = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        }),
        body = """{"username":"updated-user"}""",
        bodyType = "application/json",
        collectionId
    };

    /// <summary>An update payload (no collectionId — you can't change the collection).</summary>
    public static object UpdatePayload() => new
    {
        name = "Renamed Request",
        method = "PATCH",
        url = "https://api.example.com/users/42",
        headers = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Accept"] = "application/json"
        }),
        body = """{"role":"admin"}""",
        bodyType = "application/json"
    };

    /// <summary>Payload with empty URL — should fail validation.</summary>
    public static object RequestWithEmptyUrl(Guid collectionId) => new
    {
        name = "Bad Request",
        method = "GET",
        url = "",
        collectionId
    };

    /// <summary>Payload with invalid HTTP method — should fail validation.</summary>
    public static object RequestWithInvalidMethod(Guid collectionId) => new
    {
        name = "Bad Method",
        method = "FROBNICATE",
        url = "https://api.example.com/test",
        collectionId
    };

    /// <summary>A body that exceeds the 10MB security limit.</summary>
    public static object RequestWithOversizedBody(Guid collectionId)
    {
        // 10MB + 1 byte
        var oversizedBody = new string('x', 10 * 1024 * 1024 + 1);
        return new
        {
            name = "Oversized Body",
            method = "POST",
            url = "https://api.example.com/upload",
            body = oversizedBody,
            bodyType = "text/plain",
            collectionId
        };
    }

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

    /// <summary>Minimal shape for deserializing an ID from a created resource.</summary>
    public record IdResponse(Guid Id);

    /// <summary>Expected shape of an API request returned from the API.</summary>
    public record RequestResponse(
        Guid Id,
        Guid CollectionId,
        string Name,
        string Method,
        string Url,
        string? Headers,
        string? Body,
        string? BodyType,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    /// <summary>Expected shape of a request history entry.</summary>
    public record HistoryResponse(
        Guid Id,
        Guid RequestId,
        string Method,
        string Url,
        string? RequestHeaders,
        string? RequestBody,
        int ResponseStatus,
        string? ResponseHeaders,
        string? ResponseBody,
        long ResponseTimeMs,
        long ResponseSizeBytes,
        DateTime ExecutedAt);

    /// <summary>Expected shape of a send-request response.</summary>
    public record SendResponse(
        int ResponseStatus,
        string? ResponseHeaders,
        string? ResponseBody,
        long ResponseTimeMs,
        long ResponseSizeBytes);
}
