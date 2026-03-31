using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.Collections;

/// <summary>
/// TDD Red-phase tests for collection duplication.
/// POST /api/collections/{id}/duplicate — deep copy with new IDs.
/// All tests MUST FAIL until the duplicate endpoint is implemented.
/// </summary>
public class CollectionDuplicateTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/collections/{id}/duplicate — Deep Copy
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedDuplicate_When_CollectionDuplicated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/duplicate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        duplicate.Should().NotBeNull();
        duplicate!.Id.Should().NotBe(collectionId);
    }

    [Fact]
    public async Task Should_AppendCopySuffix_When_CollectionDuplicated()
    {
        // Create a collection with a known name
        var workspaceId = await CollectionTestData.SeedWorkspaceAsync(_client);

        var createResponse = await _client.PostAsync("/api/collections",
            CollectionTestData.JsonContent(
                CollectionTestData.CreateCollectionPayload(workspaceId, "My API Suite")));
        var created = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(createResponse);

        var response = await _client.PostAsync(
            $"/api/collections/{created!.Id}/duplicate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        duplicate!.Name.Should().Be("My API Suite (Copy)");
    }

    [Fact]
    public async Task Should_DuplicateFolders_When_CollectionHasFolders()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Auth");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Users");

        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/duplicate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);

        // Fetch the duplicate to get its full structure
        var detailResponse = await _client.GetAsync($"/api/collections/{duplicate!.Id}");
        var detail = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(detailResponse);

        detail!.Folders.Should().NotBeNull();
        detail.Folders!.Length.Should().BeGreaterThanOrEqualTo(2);
        detail.Folders.Should().Contain(f => f.Name == "Auth");
        detail.Folders.Should().Contain(f => f.Name == "Users");
    }

    [Fact]
    public async Task Should_DuplicateRequests_When_CollectionHasRequests()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Get All Users");
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Create User");

        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/duplicate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        var detailResponse = await _client.GetAsync($"/api/collections/{duplicate!.Id}");
        var detail = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(detailResponse);

        detail!.Requests.Should().NotBeNull();
        detail.Requests!.Length.Should().BeGreaterThanOrEqualTo(2);
        detail.Requests.Should().Contain(r => r.Name == "Get All Users");
        detail.Requests.Should().Contain(r => r.Name == "Create User");
    }

    [Fact]
    public async Task Should_HaveNewIds_When_Duplicated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var originalFolderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Original Folder");
        var originalRequestId = await CollectionTestData.CreateRequestAsync(
            _client, collectionId, "Original Request", originalFolderId);

        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/duplicate", null);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        var detailResponse = await _client.GetAsync($"/api/collections/{duplicate!.Id}");
        var detail = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(detailResponse);

        // All IDs should be different from originals
        detail!.Id.Should().NotBe(collectionId);

        var dupFolder = detail.Folders!.First(f => f.Name == "Original Folder");
        dupFolder.Id.Should().NotBe(originalFolderId);

        var dupRequest = dupFolder.Requests!.First(r => r.Name == "Original Request");
        dupRequest.Id.Should().NotBe(originalRequestId);
    }

    [Fact]
    public async Task Should_PreserveNestedStructure_When_Duplicated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var parentId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Parent");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Child", parentId);
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Nested Request", parentId);

        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/duplicate", null);

        var duplicate = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        var detailResponse = await _client.GetAsync($"/api/collections/{duplicate!.Id}");
        var detail = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(detailResponse);

        // Parent folder should exist with its child and request
        var dupParent = detail!.Folders!.FirstOrDefault(f => f.Name == "Parent");
        dupParent.Should().NotBeNull();
        dupParent!.SubFolders.Should().NotBeNull();
        dupParent.SubFolders!.Should().Contain(f => f.Name == "Child");
        dupParent.Requests.Should().NotBeNull();
        dupParent.Requests!.Should().Contain(r => r.Name == "Nested Request");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DuplicatingNonExistentCollection()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/collections/{nonExistentId}/duplicate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
