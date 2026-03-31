using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.Requests;

/// <summary>
/// TDD Red-phase tests for Request CRUD endpoints.
/// These tests define the contract Marcus will implement.
/// All tests MUST FAIL until endpoints are built.
/// </summary>
public class RequestCrudTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/requests — Create
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnCreatedRequest_When_ValidGetRequestProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Get Users");
        created.Method.Should().Be("GET");
        created.Url.Should().Be("https://api.example.com/users");
        created.CollectionId.Should().Be(collectionId);
    }

    [Fact]
    public async Task Should_ReturnCreatedRequest_When_PostRequestWithBodyProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidPostRequest(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created.Should().NotBeNull();
        created!.Method.Should().Be("POST");
        created.Body.Should().Contain("testuser");
        created.BodyType.Should().Be("application/json");
        created.Headers.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_SetTimestamps_When_RequestCreated()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created!.CreatedAt.Should().BeAfter(before);
        created.UpdatedAt.Should().BeAfter(before);
        created.CreatedAt.Should().BeCloseTo(created.UpdatedAt, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Should_ReturnLocationHeader_When_RequestCreated()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/requests/");
    }

    // ──────────────────────────────────────────────
    // GET /api/requests — List All
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoRequestsExist()
    {
        var response = await _client.GetAsync("/api/requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PaginatedRequests>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnAllRequests_When_MultipleExist()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create two requests
        await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidPostRequest(collectionId)));

        var response = await _client.GetAsync("/api/requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedRequests>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    private record PaginatedRequests(TestData.RequestResponse[] Items, int Page, int PageSize, int TotalCount);

    // ──────────────────────────────────────────────
    // GET /api/requests/{id} — Get by ID
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnRequest_When_ValidIdProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidPostRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var response = await _client.GetAsync($"/api/requests/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await TestData.Deserialize<TestData.RequestResponse>(response);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Create User");
        fetched.Method.Should().Be("POST");
        fetched.Url.Should().Be("https://api.example.com/users");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_RequestIdDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/requests/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // PUT /api/requests/{id} — Update
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnUpdatedRequest_When_ValidUpdateProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create first
        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Update
        var response = await _client.PutAsync($"/api/requests/{created!.Id}",
            TestData.JsonContent(TestData.UpdatePayload()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await TestData.Deserialize<TestData.RequestResponse>(response);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Renamed Request");
        updated.Method.Should().Be("PATCH");
        updated.Url.Should().Be("https://api.example.com/users/42");
    }

    [Fact]
    public async Task Should_UpdateTimestamp_When_RequestUpdated()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Small delay to ensure timestamp difference
        await Task.Delay(100);

        var response = await _client.PutAsync($"/api/requests/{created!.Id}",
            TestData.JsonContent(TestData.UpdatePayload()));

        var updated = await TestData.Deserialize<TestData.RequestResponse>(response);
        updated!.UpdatedAt.Should().BeAfter(created.UpdatedAt);
        updated.CreatedAt.Should().Be(created.CreatedAt);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PutAsync($"/api/requests/{nonExistentId}",
            TestData.JsonContent(TestData.UpdatePayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // DELETE /api/requests/{id} — Delete
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnNoContent_When_RequestDeleted()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var response = await _client.DeleteAsync($"/api/requests/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingDeletedRequest()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        await _client.DeleteAsync($"/api/requests/{created!.Id}");

        var getResponse = await _client.GetAsync($"/api/requests/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/requests/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // POST /api/requests/{id}/send — Execute Request
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnResponse_When_RequestExecuted()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var response = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<TestData.SendResponse>(response);
        result.Should().NotBeNull();
        // Real proxy engine may return status 0 with an error if the target URL is unreachable
        result!.ResponseStatus.Should().BeGreaterThanOrEqualTo(0);
        result.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Should_RecordHistory_When_RequestExecuted()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Execute the request
        await _client.PostAsync($"/api/requests/{created!.Id}/send", null);

        // Check history was recorded
        var historyResponse = await _client.GetAsync($"/api/requests/{created.Id}/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(historyResponse);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(1);
        result.Items[0].RequestId.Should().Be(created.Id);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_SendingNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PostAsync($"/api/requests/{nonExistentId}/send", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // GET /api/requests/{id}/history — Execution History
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnEmptyHistory_When_RequestNeverExecuted()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var response = await _client.GetAsync($"/api/requests/{created!.Id}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnHistoryWithTimingData_When_RequestExecutedMultipleTimes()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Execute twice
        await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        await _client.PostAsync($"/api/requests/{created.Id}/send", null);

        var response = await _client.GetAsync($"/api/requests/{created.Id}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<PaginatedHistory>(response);
        result.Should().NotBeNull();
        result!.Items.Length.Should().BeGreaterThanOrEqualTo(2);

        // Each history entry should have timing data
        foreach (var entry in result.Items)
        {
            entry.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
            entry.ExecutedAt.Should().NotBe(default);
            entry.Method.Should().NotBeNullOrEmpty();
            entry.Url.Should().NotBeNullOrEmpty();
        }
    }

    private record PaginatedHistory(TestData.HistoryResponse[] Items, int Page, int PageSize, int TotalCount);

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingHistoryForNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/requests/{nonExistentId}/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
