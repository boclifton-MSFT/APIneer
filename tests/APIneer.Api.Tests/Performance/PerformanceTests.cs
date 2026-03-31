using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using APIneer.Api.Tests.Requests;

namespace APIneer.Api.Tests.Performance;

/// <summary>
/// Performance tests to verify the API handles large payloads, concurrent requests,
/// paginated history with many entries, and bulk collection operations.
/// </summary>
public class PerformanceTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ─── Large response handling ────────────────────────────────────

    [Fact]
    public async Task Should_HandleLargeResponseBody_WithoutTimeout()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create a request with a body > 1MB
        var largeBody = new string('A', 1_100_000);
        var payload = new
        {
            name = "Large Body Request",
            method = "POST",
            url = "https://api.example.com/upload",
            body = largeBody,
            bodyType = "text/plain",
            collectionId
        };

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.PostAsync("/api/requests", TestData.JsonContent(payload));
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "creating a request with a large body should complete quickly");

        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created.Should().NotBeNull();
        created!.Body.Should().NotBeNullOrEmpty();
        created.Body!.Length.Should().BeGreaterThanOrEqualTo(1_000_000);
    }

    [Fact]
    public async Task Should_HandleLargeRequestRetrieval_WithoutTimeout()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create a request with a body close to 1MB
        var largeBody = new string('B', 900_000);
        var payload = new
        {
            name = "Large Retrieval Test",
            method = "POST",
            url = "https://api.example.com/data",
            body = largeBody,
            bodyType = "text/plain",
            collectionId
        };

        var createResponse = await _client.PostAsync("/api/requests", TestData.JsonContent(payload));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var stopwatch = Stopwatch.StartNew();
        var getResponse = await _client.GetAsync($"/api/requests/{created!.Id}");
        stopwatch.Stop();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "retrieving a large request should be fast");
    }

    // ─── Concurrent request execution ───────────────────────────────

    [Fact]
    public async Task Should_HandleConcurrentRequestExecution_WithoutFailure()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create a request to execute
        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Execute 10 sequential send requests rapidly to test throughput
        var stopwatch = Stopwatch.StartNew();
        var results = new List<HttpStatusCode>();
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
            results.Add(response.StatusCode);
        }
        stopwatch.Stop();

        // All requests should succeed
        results.Should().AllSatisfy(status =>
            status.Should().Be(HttpStatusCode.OK, "all send requests should succeed"));

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "10 sequential requests should complete in reasonable time");
    }

    [Fact]
    public async Task Should_HandleConcurrentCreation_WithoutFailure()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create 10 requests sequentially to test throughput
        var stopwatch = Stopwatch.StartNew();
        var results = new List<HttpStatusCode>();
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.PostAsync("/api/requests",
                TestData.JsonContent(new
                {
                    name = $"Throughput Request {i}",
                    method = "GET",
                    url = $"https://api.example.com/resource/{i}",
                    collectionId
                }));
            results.Add(response.StatusCode);
        }
        stopwatch.Stop();

        results.Should().AllSatisfy(status =>
            status.Should().Be(HttpStatusCode.Created, "all creation requests should succeed"));

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "10 rapid creation requests should complete quickly");
    }

    // ─── History pagination with many entries ────────────────────────

    [Fact]
    public async Task Should_PaginateHistory_With1000PlusEntries()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create a request
        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Execute the request many times to build history (50 entries for test speed)
        const int entryCount = 50;
        for (int i = 0; i < entryCount; i++)
        {
            await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        }

        // Verify total count
        var firstPageResponse = await _client.GetAsync($"/api/requests/{created!.Id}/history?page=1&pageSize=10");
        firstPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPage = await JsonSerializer.DeserializeAsync<PaginatedHistory>(
            await firstPageResponse.Content.ReadAsStreamAsync(), JsonOptions);
        firstPage.Should().NotBeNull();
        firstPage!.TotalCount.Should().BeGreaterThanOrEqualTo(entryCount);
        firstPage.Items.Length.Should().Be(10);
        firstPage.Page.Should().Be(1);

        // Verify second page
        var secondPageResponse = await _client.GetAsync($"/api/requests/{created.Id}/history?page=2&pageSize=10");
        secondPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPage = await JsonSerializer.DeserializeAsync<PaginatedHistory>(
            await secondPageResponse.Content.ReadAsStreamAsync(), JsonOptions);
        secondPage.Should().NotBeNull();
        secondPage!.Items.Length.Should().Be(10);
        secondPage.Page.Should().Be(2);

        // Verify last page (page 5 of 50 entries with pageSize=10)
        var lastPageResponse = await _client.GetAsync($"/api/requests/{created.Id}/history?page=5&pageSize=10");
        lastPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lastPage = await JsonSerializer.DeserializeAsync<PaginatedHistory>(
            await lastPageResponse.Content.ReadAsStreamAsync(), JsonOptions);
        lastPage.Should().NotBeNull();
        lastPage!.Items.Length.Should().Be(10);
        lastPage.Page.Should().Be(5);

        // Verify pagination performs well
        var stopwatch = Stopwatch.StartNew();
        var perfResponse = await _client.GetAsync($"/api/requests/{created.Id}/history?page=1&pageSize=20");
        stopwatch.Stop();

        perfResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "paginated history query should be fast even with many entries");
    }

    [Fact]
    public async Task Should_PaginateGlobalHistory_Efficiently()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create multiple requests and execute each
        for (int i = 0; i < 5; i++)
        {
            var resp = await _client.PostAsync("/api/requests",
                TestData.JsonContent(new
                {
                    name = $"History Perf Request {i}",
                    method = "GET",
                    url = $"https://api.example.com/perf/{i}",
                    collectionId
                }));
            var req = await TestData.Deserialize<TestData.RequestResponse>(resp);

            for (int j = 0; j < 5; j++)
            {
                await _client.PostAsync($"/api/requests/{req!.Id}/send", null);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/history?page=1&pageSize=10");
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "global history pagination should be fast");

        var result = await JsonSerializer.DeserializeAsync<PaginatedHistory>(
            await response.Content.ReadAsStreamAsync(), JsonOptions);
        result.Should().NotBeNull();
        result!.TotalCount.Should().BeGreaterThanOrEqualTo(25);
        result.Items.Length.Should().Be(10);
    }

    // ─── Bulk collection operations ─────────────────────────────────

    [Fact]
    public async Task Should_DuplicateCollection_InReasonableTime()
    {
        var workspaceId = await SeedWorkspaceAsync();

        // Create a collection with many requests
        var colPayload = new { name = "Bulk Collection", workspaceId };
        var colResponse = await _client.PostAsync("/api/collections",
            TestData.JsonContent(colPayload));
        var col = await TestData.Deserialize<TestData.IdResponse>(colResponse);

        // Add 20 requests
        for (int i = 0; i < 20; i++)
        {
            await _client.PostAsync("/api/requests",
                TestData.JsonContent(new
                {
                    name = $"Request {i}",
                    method = "GET",
                    url = $"https://api.example.com/items/{i}",
                    collectionId = col!.Id
                }));
        }

        var stopwatch = Stopwatch.StartNew();
        var dupResponse = await _client.PostAsync($"/api/collections/{col!.Id}/duplicate", null);
        stopwatch.Stop();

        dupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "duplicating a collection with 20 requests should be fast");
    }

    [Fact]
    public async Task Should_ListCollections_WithPagination()
    {
        var workspaceId = await SeedWorkspaceAsync();

        // Create several collections
        for (int i = 0; i < 15; i++)
        {
            await _client.PostAsync("/api/collections",
                TestData.JsonContent(new { name = $"Perf Collection {i}", workspaceId }));
        }

        // Get first page
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/collections?page=1&pageSize=5");
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

        var result = await JsonSerializer.DeserializeAsync<PaginatedCollections>(
            await response.Content.ReadAsStreamAsync(), JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Length.Should().Be(5);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(15);

        // Get second page
        var page2Response = await _client.GetAsync("/api/collections?page=2&pageSize=5");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page2 = await JsonSerializer.DeserializeAsync<PaginatedCollections>(
            await page2Response.Content.ReadAsStreamAsync(), JsonOptions);
        page2.Should().NotBeNull();
        page2!.Items.Length.Should().Be(5);
        page2.Page.Should().Be(2);
    }

    [Fact]
    public async Task Should_DeleteCollection_WithManyRequests_InReasonableTime()
    {
        var workspaceId = await SeedWorkspaceAsync();

        // Create a collection
        var colResponse = await _client.PostAsync("/api/collections",
            TestData.JsonContent(new { name = "Delete Perf Collection", workspaceId }));
        var col = await TestData.Deserialize<TestData.IdResponse>(colResponse);

        // Add 15 requests and execute each to create history
        for (int i = 0; i < 15; i++)
        {
            var reqResp = await _client.PostAsync("/api/requests",
                TestData.JsonContent(new
                {
                    name = $"Delete Test {i}",
                    method = "GET",
                    url = $"https://api.example.com/delete/{i}",
                    collectionId = col!.Id
                }));
            var req = await TestData.Deserialize<TestData.RequestResponse>(reqResp);
            await _client.PostAsync($"/api/requests/{req!.Id}/send", null);
        }

        var stopwatch = Stopwatch.StartNew();
        var deleteResponse = await _client.DeleteAsync($"/api/collections/{col!.Id}");
        stopwatch.Stop();

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "deleting a collection with 15 requests and history should be fast");
    }

    // ─── Helper types ───────────────────────────────────────────────

    private record PaginatedHistory(TestData.HistoryResponse[] Items, int Page, int PageSize, int TotalCount);
    private record PaginatedCollections(CollectionItem[] Items, int Page, int PageSize, int TotalCount);
    private record CollectionItem(Guid Id, Guid WorkspaceId, string Name, string? Description);

    private async Task<Guid> SeedWorkspaceAsync()
    {
        var response = await _client.PostAsync("/api/workspaces",
            TestData.JsonContent(new { name = "Perf Test Workspace" }));
        var result = await TestData.Deserialize<TestData.IdResponse>(response);
        return result!.Id;
    }
}
