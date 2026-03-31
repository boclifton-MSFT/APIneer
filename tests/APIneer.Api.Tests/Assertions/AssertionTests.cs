using System.Net;
using System.Text.Json;
using APIneer.Api.Tests.Requests;
using FluentAssertions;

namespace APIneer.Api.Tests.Assertions;

/// <summary>
/// TDD Red-phase tests for Request Assertion endpoints.
/// These endpoints do NOT exist yet — every test MUST fail.
///
/// Contract:
///   POST /api/requests/{id}/assertions  — add an assertion
///   GET  /api/requests/{id}/assertions  — list assertions for a request
///   POST /api/requests/{id}/test        — run request and evaluate all assertions
///
/// Assertion types:
///   - status_equals   : response status code must equal the expected value
///   - body_contains   : response body must contain the expected string
///   - header_exists   : a specific response header must be present
/// </summary>
public class AssertionTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ── DTOs ─────────────────────────────────────────────────────

    public record CreateAssertionDto(
        string Type,
        string Expected);

    public record AssertionResponse(
        Guid Id,
        Guid RequestId,
        string Type,
        string Expected,
        DateTime CreatedAt);

    public record AssertionResultDto(
        string Type,
        string Expected,
        bool Passed,
        string? Actual);

    public record TestRunResponse(
        Guid RequestId,
        int ResponseStatus,
        bool AllPassed,
        AssertionResultDto[] Results);

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateRequestAsync(Guid collectionId)
    {
        var payload = new
        {
            name = "Assertable Request",
            method = "GET",
            url = "https://api.example.com/users",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));
        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        return created!.Id;
    }

    private async Task<HttpResponseMessage> AddAssertionAsync(
        Guid requestId, string type, string expected)
    {
        var dto = new CreateAssertionDto(type, expected);
        return await _client.PostAsync(
            $"/api/requests/{requestId}/assertions",
            TestData.JsonContent(dto));
    }

    // ──────────────────────────────────────────────
    // POST /api/requests/{id}/assertions — Add assertion
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_CreateStatusAssertion_When_ValidPayloadProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        var response = await AddAssertionAsync(requestId, "status_equals", "200");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<AssertionResponse>(response);
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.RequestId.Should().Be(requestId);
        created.Type.Should().Be("status_equals");
        created.Expected.Should().Be("200");
    }

    [Fact]
    public async Task Should_CreateBodyContainsAssertion_When_ValidPayloadProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        var response = await AddAssertionAsync(requestId, "body_contains", "OK");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<AssertionResponse>(response);
        created.Should().NotBeNull();
        created!.Type.Should().Be("body_contains");
        created.Expected.Should().Be("OK");
    }

    [Fact]
    public async Task Should_CreateHeaderExistsAssertion_When_ValidPayloadProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        var response = await AddAssertionAsync(requestId, "header_exists", "Content-Type");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await TestData.Deserialize<AssertionResponse>(response);
        created.Should().NotBeNull();
        created!.Type.Should().Be("header_exists");
        created.Expected.Should().Be("Content-Type");
    }

    [Fact]
    public async Task Should_Return404_When_AddingAssertionToNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await AddAssertionAsync(nonExistentId, "status_equals", "200");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_AllowMultipleAssertions_OnSameRequest()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        var r1 = await AddAssertionAsync(requestId, "status_equals", "200");
        var r2 = await AddAssertionAsync(requestId, "body_contains", "message");
        var r3 = await AddAssertionAsync(requestId, "header_exists", "Content-Type");

        r1.StatusCode.Should().Be(HttpStatusCode.Created);
        r2.StatusCode.Should().Be(HttpStatusCode.Created);
        r3.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ──────────────────────────────────────────────
    // GET /api/requests/{id}/assertions — List assertions
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnAllAssertions_When_ListingForRequest()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        await AddAssertionAsync(requestId, "status_equals", "200");
        await AddAssertionAsync(requestId, "body_contains", "OK");

        var response = await _client.GetAsync($"/api/requests/{requestId}/assertions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var assertions = await TestData.Deserialize<AssertionResponse[]>(response);
        assertions.Should().NotBeNull();
        assertions!.Length.Should().Be(2);
        assertions.Should().AllSatisfy(a => a.RequestId.Should().Be(requestId));
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoAssertionsExist()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        var response = await _client.GetAsync($"/api/requests/{requestId}/assertions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var assertions = await TestData.Deserialize<AssertionResponse[]>(response);
        assertions.Should().NotBeNull();
        assertions.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Return404_When_ListingAssertionsForNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/requests/{nonExistentId}/assertions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // POST /api/requests/{id}/test — Run & evaluate
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_RunTestAndReturnResults_When_AssertionsExist()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        await AddAssertionAsync(requestId, "status_equals", "200");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();
        result!.RequestId.Should().Be(requestId);
        result.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Return404_When_TestingNonExistentRequest()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/requests/{nonExistentId}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────
    // Status code assertion: pass/fail
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PassStatusAssertion_When_StatusMatches()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns status 200
        await AddAssertionAsync(requestId, "status_equals", "200");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();
        result!.AllPassed.Should().BeTrue();

        var statusResult = result.Results.First(r => r.Type == "status_equals");
        statusResult.Passed.Should().BeTrue();
        statusResult.Expected.Should().Be("200");
        statusResult.Actual.Should().Be("200");
    }

    [Fact]
    public async Task Should_FailStatusAssertion_When_StatusDoesNotMatch()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns 200, but we expect 404
        await AddAssertionAsync(requestId, "status_equals", "404");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();
        result!.AllPassed.Should().BeFalse();

        var statusResult = result.Results.First(r => r.Type == "status_equals");
        statusResult.Passed.Should().BeFalse();
        statusResult.Expected.Should().Be("404");
        statusResult.Actual.Should().Be("200");
    }

    // ──────────────────────────────────────────────
    // Body contains assertion: pass/fail
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PassBodyContainsAssertion_When_BodyContainsString()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns body: {"message":"OK"}
        await AddAssertionAsync(requestId, "body_contains", "OK");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();

        var bodyResult = result!.Results.First(r => r.Type == "body_contains");
        bodyResult.Passed.Should().BeTrue();
        bodyResult.Expected.Should().Be("OK");
    }

    [Fact]
    public async Task Should_FailBodyContainsAssertion_When_BodyDoesNotContainString()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns body: {"message":"OK"} — "NotPresent" is not in it
        await AddAssertionAsync(requestId, "body_contains", "NotPresent");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();

        var bodyResult = result!.Results.First(r => r.Type == "body_contains");
        bodyResult.Passed.Should().BeFalse();
        bodyResult.Expected.Should().Be("NotPresent");
    }

    // ──────────────────────────────────────────────
    // Header exists assertion: pass/fail
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PassHeaderExistsAssertion_When_HeaderIsPresent()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns headers: {"Content-Type":"application/json"}
        await AddAssertionAsync(requestId, "header_exists", "Content-Type");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();

        var headerResult = result!.Results.First(r => r.Type == "header_exists");
        headerResult.Passed.Should().BeTrue();
        headerResult.Expected.Should().Be("Content-Type");
    }

    [Fact]
    public async Task Should_FailHeaderExistsAssertion_When_HeaderIsNotPresent()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        // The stub /send returns headers: {"Content-Type":"application/json"} — no X-Custom header
        await AddAssertionAsync(requestId, "header_exists", "X-Custom-Missing");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();

        var headerResult = result!.Results.First(r => r.Type == "header_exists");
        headerResult.Passed.Should().BeFalse();
        headerResult.Expected.Should().Be("X-Custom-Missing");
    }

    // ──────────────────────────────────────────────
    // Mixed assertions: combined pass/fail
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReportAllPassed_When_AllAssertionsPass()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        await AddAssertionAsync(requestId, "status_equals", "200");
        await AddAssertionAsync(requestId, "body_contains", "OK");
        await AddAssertionAsync(requestId, "header_exists", "Content-Type");

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();
        result!.AllPassed.Should().BeTrue();
        result.Results.Length.Should().Be(3);
        result.Results.Should().AllSatisfy(r => r.Passed.Should().BeTrue());
    }

    [Fact]
    public async Task Should_ReportNotAllPassed_When_AnyAssertionFails()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestAsync(collectionId);

        await AddAssertionAsync(requestId, "status_equals", "200");  // will pass
        await AddAssertionAsync(requestId, "body_contains", "MISSING_STRING");  // will fail

        var response = await _client.PostAsync(
            $"/api/requests/{requestId}/test", null);

        var result = await TestData.Deserialize<TestRunResponse>(response);
        result.Should().NotBeNull();
        result!.AllPassed.Should().BeFalse();
        result.Results.Length.Should().Be(2);

        // One passes, one fails
        result.Results.Should().Contain(r => r.Passed);
        result.Results.Should().Contain(r => !r.Passed);
    }
}
