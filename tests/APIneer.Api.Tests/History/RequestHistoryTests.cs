using System.Net;
using System.Text.Json;
using APIneer.Api.Tests.Requests;
using FluentAssertions;

namespace APIneer.Api.Tests.History;

/// <summary>
/// TDD Red-phase tests for global Request History endpoints.
/// These endpoints do NOT exist yet — every test MUST fail.
///
/// Contract:
///   GET    /api/history                     — list all history (paginated)
///   GET    /api/history?requestId={id}      — filter by originating request
///   GET    /api/history?method=GET          — filter by HTTP method
///   GET    /api/history?status=200          — filter by response status code
///   GET    /api/history?from=...&to=...     — filter by date range
///   DELETE /api/history                     — clear all history
/// </summary>
public class RequestHistoryTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── DTOs for the new global history response shape ───────────

    public record GlobalHistoryEntry(
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

    public record PaginatedHistory(
        GlobalHistoryEntry[] Items,
        int Page,
        int PageSize,
        int TotalCount);

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a request and executes it so history is generated.
    /// Returns the requestId.
    /// </summary>
    private async Task<Guid> CreateAndExecuteRequestAsync(
        Guid collectionId,
        string method = "GET",
        string url = "https://api.example.com/users")
    {
        var payload = new
        {
            name = $"Test {method}",
            method,
            url,
            collectionId
        };

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        return created.Id;
    }

    // ──────────────────────────────────────────────
    // GET /api/history — List all history entries
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnAllHistory_When_GetHistoryEndpointCalled()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/items");

        var response = await _client.GetAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoHistoryExists()
    {
        var response = await _client.GetAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ──────────────────────────────────────────────
    // GET /api/history?requestId={id} — Filter by request
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_FilterByRequestId_When_RequestIdQueryProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var targetId = await CreateAndExecuteRequestAsync(collectionId);
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/other");

        var response = await _client.GetAsync($"/api/history?requestId={targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(h => h.RequestId.Should().Be(targetId));
        result.Items.Length.Should().Be(1);
    }

    // ──────────────────────────────────────────────
    // GET /api/history?method=GET — Filter by HTTP method
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_FilterByMethod_When_MethodQueryProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId, "GET");
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/items");

        var response = await _client.GetAsync("/api/history?method=GET");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(h => h.Method.Should().Be("GET"));
        result.Items.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_FilterByMethodWithNoMatches()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId, "GET");

        var response = await _client.GetAsync("/api/history?method=DELETE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // GET /api/history?status=200 — Filter by status code
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_FilterByStatus_When_StatusQueryProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        var response = await _client.GetAsync("/api/history?status=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(h => h.ResponseStatus.Should().Be(200));
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_FilterByStatusWithNoMatches()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        var response = await _client.GetAsync("/api/history?status=500");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // GET /api/history?from=...&to=... — Filter by date range
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_FilterByDateRange_When_FromAndToProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        var from = DateTime.UtcNow.AddHours(-1).ToString("o");
        var to = DateTime.UtcNow.AddHours(1).ToString("o");

        var response = await _client.GetAsync($"/api/history?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_DateRangeExcludesAllEntries()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        // Date range far in the past — nothing should match
        var response = await _client.GetAsync("/api/history?from=2020-01-01&to=2020-01-02");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // History includes request & response snapshots
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_IncludeRequestSnapshot_InHistoryEntry()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId, "GET", "https://api.example.com/snapshot");

        var response = await _client.GetAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        var entry = result!.Items.First();
        entry.Method.Should().Be("GET");
        entry.Url.Should().Be("https://api.example.com/snapshot");
    }

    [Fact]
    public async Task Should_IncludeResponseSnapshot_InHistoryEntry()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        var response = await _client.GetAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        var entry = result!.Items.First();
        entry.ResponseStatus.Should().BeGreaterThanOrEqualTo(0);
        // Body may be null when the proxy returns an error (unreachable URL)
        entry.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
        entry.ResponseSizeBytes.Should().BeGreaterThanOrEqualTo(0);
    }

    // ──────────────────────────────────────────────
    // Pagination: page, pageSize params
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnPaginatedResults_When_PageAndPageSizeProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create 3 history entries
        await CreateAndExecuteRequestAsync(collectionId, "GET", "https://api.example.com/a");
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/b");
        await CreateAndExecuteRequestAsync(collectionId, "PUT", "https://api.example.com/c");

        var response = await _client.GetAsync("/api/history?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_ReturnSecondPage_When_Page2Requested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create 3 history entries
        await CreateAndExecuteRequestAsync(collectionId, "GET", "https://api.example.com/a");
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/b");
        await CreateAndExecuteRequestAsync(collectionId, "PUT", "https://api.example.com/c");

        var response = await _client.GetAsync("/api/history?page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().Be(1);
        result.Page.Should().Be(2);
    }

    [Fact]
    public async Task Should_UseDefaultPagination_When_NoPageParamsProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);

        var response = await _client.GetAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Page.Should().BeGreaterThanOrEqualTo(1);
        result.PageSize.Should().BeGreaterThan(0);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/history — Clear all history
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ClearAllHistory_When_DeleteHistoryCalled()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        await CreateAndExecuteRequestAsync(collectionId);
        await CreateAndExecuteRequestAsync(collectionId, "POST", "https://api.example.com/items");

        // Verify history exists
        var beforeResponse = await _client.GetAsync("/api/history");
        var before = await TestData.Deserialize<PaginatedHistory>(beforeResponse);
        before!.TotalCount.Should().BeGreaterThanOrEqualTo(2);

        // Clear history
        var deleteResponse = await _client.DeleteAsync("/api/history");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify history is empty
        var afterResponse = await _client.GetAsync("/api/history");
        var after = await TestData.Deserialize<PaginatedHistory>(afterResponse);
        after!.Items.Should().BeEmpty();
        after.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNoContent_When_ClearingEmptyHistory()
    {
        var response = await _client.DeleteAsync("/api/history");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
