using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.Collections;

/// <summary>
/// TDD Red-phase tests for Collection Folder endpoints.
/// Covers folder CRUD, nesting, moving requests between folders, and cascade delete.
/// All tests MUST FAIL until endpoints are implemented.
/// </summary>
public class FolderTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/collections/{id}/folders — Create Folder
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedFolder_When_ValidPayloadProvided()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var payload = new { name = "Auth Endpoints" };
        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/folders",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await CollectionTestData.Deserialize<CollectionTestData.FolderResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Auth Endpoints");
        created.CollectionId.Should().Be(collectionId);
        created.ParentFolderId.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnLocationHeader_When_FolderCreated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var payload = new { name = "Users" };
        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/folders",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_FolderNameMissing()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        var payload = new { description = "no name" };
        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/folders",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_CreatingFolderInNonExistentCollection()
    {
        var nonExistentId = Guid.NewGuid();

        var payload = new { name = "Orphan Folder" };
        var response = await _client.PostAsync(
            $"/api/collections/{nonExistentId}/folders",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // Nested Folders (subfolder of a folder)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_CreateNestedFolder_When_ParentFolderIdProvided()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var parentFolderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Parent");

        var payload = new { name = "Child Folder", parentFolderId };
        var response = await _client.PostAsync(
            $"/api/collections/{collectionId}/folders",
            CollectionTestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var child = await CollectionTestData.Deserialize<CollectionTestData.FolderResponse>(response);
        child.Should().NotBeNull();
        child!.ParentFolderId.Should().Be(parentFolderId);
        child.Name.Should().Be("Child Folder");
        child.CollectionId.Should().Be(collectionId);
    }

    [Fact]
    public async Task Should_ReturnNestedStructure_When_GettingCollectionWithNestedFolders()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var parentId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Root Folder");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Nested Folder", parentId);

        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        collection!.Folders.Should().NotBeNull();

        // The root-level folders should contain the parent, which should have a subfolder
        var rootFolder = collection.Folders!.FirstOrDefault(f => f.Name == "Root Folder");
        rootFolder.Should().NotBeNull();
        rootFolder!.SubFolders.Should().NotBeNull();
        rootFolder.SubFolders!.Length.Should().BeGreaterThanOrEqualTo(1);
        rootFolder.SubFolders.Should().Contain(f => f.Name == "Nested Folder");
    }

    // ──────────────────────────────────────────────
    // Deep Nesting (3+ levels)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_SupportDeepNesting_When_ThreeLevelsOfFolders()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        // Level 1
        var level1 = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Level 1");
        // Level 2
        var level2 = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Level 2", level1);
        // Level 3
        var level3 = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Level 3", level2);

        // Add a request at the deepest level
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Deep Request", level3);

        // Fetch the collection and verify the nesting
        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        var l1 = collection!.Folders!.FirstOrDefault(f => f.Name == "Level 1");
        l1.Should().NotBeNull();

        var l2 = l1!.SubFolders!.FirstOrDefault(f => f.Name == "Level 2");
        l2.Should().NotBeNull();

        var l3 = l2!.SubFolders!.FirstOrDefault(f => f.Name == "Level 3");
        l3.Should().NotBeNull();

        l3!.Requests.Should().NotBeNull();
        l3.Requests!.Should().Contain(r => r.Name == "Deep Request");
    }

    // ──────────────────────────────────────────────
    // PATCH /api/requests/{id}/move — Move Request Between Folders
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_MoveRequest_When_ValidMovePayloadProvided()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var folderA = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Folder A");
        var folderB = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Folder B");

        // Create request in Folder A
        var requestId = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Movable Request", folderA);

        // Move to Folder B
        var movePayload = new { folderId = folderB };
        var response = await _client.PatchAsync(
            $"/api/requests/{requestId}/move",
            CollectionTestData.JsonContent(movePayload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the request is now in Folder B
        var requestResponse = await _client.GetAsync($"/api/requests/{requestId}");
        var request = await CollectionTestData.Deserialize<CollectionTestData.RequestSummaryResponse>(requestResponse);
        request!.FolderId.Should().Be(folderB);
    }

    [Fact]
    public async Task Should_MoveRequestToRoot_When_FolderIdIsNull()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var folder = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Some Folder");

        // Create request in a folder
        var requestId = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Root-bound Request", folder);

        // Move to collection root (no folder)
        var movePayload = new { folderId = (Guid?)null };
        var response = await _client.PatchAsync(
            $"/api/requests/{requestId}/move",
            CollectionTestData.JsonContent(movePayload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the request is now at root level
        var requestResponse = await _client.GetAsync($"/api/requests/{requestId}");
        var request = await CollectionTestData.Deserialize<CollectionTestData.RequestSummaryResponse>(requestResponse);
        request!.FolderId.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // DELETE folder — cascades to child requests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_CascadeDeleteRequests_When_FolderDeleted()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var folderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Doomed Folder");
        var requestId = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Doomed Request", folderId);

        // Delete the folder
        var deleteResponse = await _client.DeleteAsync(
            $"/api/collections/{collectionId}/folders/{folderId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The request should also be deleted
        var requestResponse = await _client.GetAsync($"/api/requests/{requestId}");
        requestResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_CascadeDeleteSubfolders_When_ParentFolderDeleted()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var parentId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Parent To Delete");
        var childId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Child Of Parent", parentId);
        var requestId = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Grandchild Request", childId);

        // Delete the parent folder
        await _client.DeleteAsync($"/api/collections/{collectionId}/folders/{parentId}");

        // The child folder's request should also be gone
        var requestResponse = await _client.GetAsync($"/api/requests/{requestId}");
        requestResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The collection itself should still exist
        var collectionResponse = await _client.GetAsync($"/api/collections/{collectionId}");
        collectionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
