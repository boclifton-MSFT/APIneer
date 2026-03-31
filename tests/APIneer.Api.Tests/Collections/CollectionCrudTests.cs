using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.Collections;

/// <summary>
/// TDD Red-phase tests for Collection CRUD endpoints.
/// All tests MUST FAIL until the collection endpoints are implemented.
/// </summary>
public class CollectionCrudTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/collections — Create
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedCollection_When_ValidPayloadProvided()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var response = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "My API Collection", "Testing APIs")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("My API Collection");
        created.Description.Should().Be("Testing APIs");
        created.WorkspaceId.Should().Be(workspaceId);
    }

    [Fact]
    public async Task Should_SetTimestamps_When_CollectionCreated()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        created!.CreatedAt.Should().BeAfter(before);
        created.UpdatedAt.Should().BeAfter(before);
        created.CreatedAt.Should().BeCloseTo(created.UpdatedAt, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Should_ReturnLocationHeader_When_CollectionCreated()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var response = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/collections/");
    }

    [Fact]
    public async Task Should_AllowNullDescription_When_Creating()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var response = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "No Description")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        created!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_NameMissing()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var payload = new { description = "No name", workspaceId };
        var response = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────
    // GET /api/collections — List All
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoCollectionsExist()
    {
        var response = await _client.GetAsync("/api/collections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PaginatedCollections>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnAllCollections_When_MultipleExist()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "Collection A")));
        await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "Collection B")));

        var response = await _client.GetAsync("/api/collections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await CollectionTestData.Deserialize<PaginatedCollections>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    private record PaginatedCollections(CollectionTestData.CollectionResponse[] Items, int Page, int PageSize, int TotalCount);

    // ──────────────────────────────────────────────
    // GET /api/collections/{id} — Get Single (with nested data)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCollection_When_ValidIdProvided()
    {
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var createResponse = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "Detail Collection", "For detail test")));
        var created = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(createResponse);

        var response = await _client.GetAsync($"/api/collections/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Detail Collection");
        fetched.Description.Should().Be("For detail test");
    }

    [Fact]
    public async Task Should_IncludeFoldersAndRequests_When_GettingSingleCollection()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        // Create a folder and a request in the collection
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Auth Folder");
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Root Request");

        var response = await _client.GetAsync($"/api/collections/{collectionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        fetched!.Folders.Should().NotBeNull();
        fetched.Folders!.Length.Should().BeGreaterThanOrEqualTo(1);
        fetched.Folders.Should().Contain(f => f.Name == "Auth Folder");

        fetched.Requests.Should().NotBeNull();
        fetched.Requests!.Length.Should().BeGreaterThanOrEqualTo(1);
        fetched.Requests.Should().Contain(r => r.Name == "Root Request");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_CollectionIdDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/collections/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // PUT /api/collections/{id} — Update
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnUpdatedCollection_When_ValidUpdateProvided()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var response = await _client.PutAsync($"/api/collections/{collectionId}",
            CollectionTestData.JsonContent(
                CollectionTestData.UpdateCollectionPayload("Renamed Collection", "New description")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Renamed Collection");
        updated.Description.Should().Be("New description");
    }

    [Fact]
    public async Task Should_UpdateTimestamp_When_CollectionUpdated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        // Small delay to ensure timestamp difference
        await Task.Delay(100);

        var response = await _client.PutAsync($"/api/collections/{collectionId}",
            CollectionTestData.JsonContent(
                CollectionTestData.UpdateCollectionPayload()));

        var updated = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        updated!.UpdatedAt.Should().BeAfter(updated.CreatedAt);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingNonExistentCollection()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PutAsync($"/api/collections/{nonExistentId}",
            CollectionTestData.JsonContent(
                CollectionTestData.UpdateCollectionPayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/collections/{id} — Delete (cascade)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnNoContent_When_CollectionDeleted()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var response = await _client.DeleteAsync($"/api/collections/{collectionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingDeletedCollection()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        await _client.DeleteAsync($"/api/collections/{collectionId}");

        var getResponse = await _client.GetAsync($"/api/collections/{collectionId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_CascadeDeleteFoldersAndRequests_When_CollectionDeleted()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        // Create folder and request inside the collection
        var folderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Cascade Folder");
        var requestId = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Cascade Request", folderId);

        // Delete the collection
        await _client.DeleteAsync($"/api/collections/{collectionId}");

        // The request should no longer be accessible
        var requestResponse = await _client.GetAsync($"/api/requests/{requestId}");
        requestResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingNonExistentCollection()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/collections/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
