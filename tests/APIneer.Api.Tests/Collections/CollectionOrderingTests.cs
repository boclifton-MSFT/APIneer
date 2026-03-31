using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.Collections;

/// <summary>
/// TDD Red-phase tests for request and folder ordering within collections.
/// Covers sort order, reordering, and folder ordering.
/// All tests MUST FAIL until ordering endpoints are implemented.
/// </summary>
public class CollectionOrderingTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // Requests within a folder have sort order
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_AssignSortOrder_When_RequestsCreatedInFolder()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var folderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Ordered Folder");

        // Create three requests in the same folder
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "First Request", folderId);
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Second Request", folderId);
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Third Request", folderId);

        // Fetch the collection and check order
        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);
        var folder = collection!.Folders!.First(f => f.Name == "Ordered Folder");
        var requests = folder.Requests!.OrderBy(r => r.SortOrder).ToArray();

        requests.Length.Should().Be(3);
        requests[0].Name.Should().Be("First Request");
        requests[1].Name.Should().Be("Second Request");
        requests[2].Name.Should().Be("Third Request");

        // Sort orders should be sequential
        requests[0].SortOrder.Should().BeLessThan(requests[1].SortOrder);
        requests[1].SortOrder.Should().BeLessThan(requests[2].SortOrder);
    }

    [Fact]
    public async Task Should_AssignSortOrder_When_RequestsCreatedAtCollectionRoot()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        // Create requests at collection root (no folder)
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Root First");
        await CollectionTestData.CreateRequestAsync(_client, collectionId, "Root Second");

        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);

        var rootRequests = collection!.Requests!.OrderBy(r => r.SortOrder).ToArray();
        rootRequests.Length.Should().BeGreaterThanOrEqualTo(2);
        rootRequests[0].SortOrder.Should().BeLessThan(rootRequests[1].SortOrder);
    }

    // ──────────────────────────────────────────────
    // PATCH /api/collections/{id}/reorder — Reorder Requests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReorderRequests_When_NewOrderProvided()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);
        var folderId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Reorder Folder");

        var req1 = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Alpha", folderId);
        var req2 = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Beta", folderId);
        var req3 = await CollectionTestData.CreateRequestAsync(_client, collectionId, "Gamma", folderId);

        // Reorder: Gamma, Alpha, Beta
        var reorderPayload = new { itemIds = new[] { req3, req1, req2 } };
        var response = await _client.PatchAsync(
            $"/api/collections/{collectionId}/reorder",
            CollectionTestData.JsonContent(reorderPayload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the new order
        var collectionResponse = await _client.GetAsync($"/api/collections/{collectionId}");
        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(collectionResponse);
        var folder = collection!.Folders!.First(f => f.Name == "Reorder Folder");
        var requests = folder.Requests!.OrderBy(r => r.SortOrder).ToArray();

        requests[0].Name.Should().Be("Gamma");
        requests[1].Name.Should().Be("Alpha");
        requests[2].Name.Should().Be("Beta");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ReorderingNonExistentCollection()
    {
        var nonExistentId = Guid.NewGuid();

        var reorderPayload = new { itemIds = new[] { Guid.NewGuid() } };
        var response = await _client.PatchAsync(
            $"/api/collections/{nonExistentId}/reorder",
            CollectionTestData.JsonContent(reorderPayload));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // Folders have sort order within collection
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_AssignSortOrder_When_FoldersCreated()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Folder A");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Folder B");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Folder C");

        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);

        var folders = collection!.Folders!
            .Where(f => f.ParentFolderId == null)
            .OrderBy(f => f.SortOrder)
            .ToArray();

        folders.Length.Should().BeGreaterThanOrEqualTo(3);

        // Sort orders should be sequential
        folders[0].SortOrder.Should().BeLessThan(folders[1].SortOrder);
        folders[1].SortOrder.Should().BeLessThan(folders[2].SortOrder);
    }

    [Fact]
    public async Task Should_MaintainFolderOrder_When_FolderDeleted()
    {
        var collectionId = await CollectionTestData.SeedCollectionAsync(_client);

        await CollectionTestData.CreateFolderAsync(_client, collectionId, "First Folder");
        var middleId = await CollectionTestData.CreateFolderAsync(_client, collectionId, "Middle Folder");
        await CollectionTestData.CreateFolderAsync(_client, collectionId, "Last Folder");

        // Delete the middle folder
        await _client.DeleteAsync($"/api/collections/{collectionId}/folders/{middleId}");

        // Remaining folders should still be ordered
        var response = await _client.GetAsync($"/api/collections/{collectionId}");
        var collection = await CollectionTestData.Deserialize<CollectionTestData.CollectionResponse>(response);

        var folders = collection!.Folders!
            .Where(f => f.ParentFolderId == null)
            .OrderBy(f => f.SortOrder)
            .ToArray();

        folders.Length.Should().Be(2);
        folders[0].Name.Should().Be("First Folder");
        folders[1].Name.Should().Be("Last Folder");
        folders[0].SortOrder.Should().BeLessThan(folders[1].SortOrder);
    }
}
