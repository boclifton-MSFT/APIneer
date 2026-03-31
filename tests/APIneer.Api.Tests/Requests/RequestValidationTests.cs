using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.Requests;

/// <summary>
/// TDD Red-phase tests for Request validation rules.
/// Validates input constraints and security limits from the security architecture doc.
/// </summary>
public class RequestValidationTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // Empty/missing URL → Allowed (draft requests)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_AcceptRequest_When_UrlIsEmpty()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.RequestWithEmptyUrl(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_AcceptRequest_When_UrlIsMissing()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var payload = new { name = "No URL", method = "GET", collectionId };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ──────────────────────────────────────────────
    // Invalid HTTP Method → 400 Bad Request
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("FROBNICATE")]
    [InlineData("YEET")]
    [InlineData("")]
    [InlineData("123")]
    public async Task Should_ReturnBadRequest_When_HttpMethodIsInvalid(string method)
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var payload = new
        {
            name = "Bad Method",
            method,
            url = "https://api.example.com/test",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Should_AcceptRequest_When_HttpMethodIsValid(string method)
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var payload = new
        {
            name = $"Valid {method}",
            method,
            url = "https://api.example.com/test",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ──────────────────────────────────────────────
    // Non-existent Request ID → 404 Not Found
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnNotFound_When_GetWithNonExistentId()
    {
        var response = await _client.GetAsync($"/api/requests/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_PutWithNonExistentId()
    {
        var response = await _client.PutAsync($"/api/requests/{Guid.NewGuid()}",
            TestData.JsonContent(TestData.UpdatePayload()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeleteWithNonExistentId()
    {
        var response = await _client.DeleteAsync($"/api/requests/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_SendWithNonExistentId()
    {
        var response = await _client.PostAsync($"/api/requests/{Guid.NewGuid()}/send", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_HistoryWithNonExistentId()
    {
        var response = await _client.GetAsync($"/api/requests/{Guid.NewGuid()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // Body Too Large → 413 Payload Too Large
    // Security doc: max request body = 10 MB
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnPayloadTooLarge_When_BodyExceeds10MB()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.RequestWithOversizedBody(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    // ──────────────────────────────────────────────
    // Name required
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnBadRequest_When_NameIsMissing()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var payload = new
        {
            method = "GET",
            url = "https://api.example.com/test",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────
    // Missing CollectionId → Auto-assigned to Default
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_AutoAssignCollection_When_CollectionIdIsMissing()
    {
        var payload = new
        {
            name = "Orphan Request",
            method = "GET",
            url = "https://api.example.com/test"
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
