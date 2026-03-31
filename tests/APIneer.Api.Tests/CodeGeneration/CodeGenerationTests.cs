using System.Net;
using System.Text.Json;
using APIneer.Api.Tests.Requests;
using FluentAssertions;

namespace APIneer.Api.Tests.CodeGeneration;

/// <summary>
/// TDD Red-phase tests for Code Generation endpoints.
/// These endpoints do NOT exist yet — every test MUST fail.
///
/// Contract:
///   GET /api/requests/{id}/code?language=javascript-fetch   — generates fetch code
///   GET /api/requests/{id}/code?language=javascript-axios    — generates axios code
///   GET /api/requests/{id}/code?language=python-requests     — generates Python requests code
///   GET /api/requests/{id}/code?language=csharp-httpclient   — generates C# HttpClient code
///   GET /api/requests/{id}/code?language=curl                — generates cURL command
///   Invalid language → 400
/// </summary>
public class CodeGenerationTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ── DTOs ─────────────────────────────────────────────────────

    public record CodeGenerationResponse(
        string Language,
        string Code,
        Guid RequestId);

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a request with headers and body, returns its ID.
    /// </summary>
    private async Task<Guid> CreateRequestWithBodyAsync(Guid collectionId)
    {
        var payload = new
        {
            name = "Create User",
            method = "POST",
            url = "https://api.example.com/users",
            headers = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Authorization"] = "Bearer test-token"
            }),
            body = """{"username":"testuser","email":"test@example.com"}""",
            bodyType = "application/json",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));
        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        return created!.Id;
    }

    /// <summary>
    /// Creates a simple GET request with no body, returns its ID.
    /// </summary>
    private async Task<Guid> CreateSimpleGetRequestAsync(Guid collectionId)
    {
        var payload = new
        {
            name = "Get Users",
            method = "GET",
            url = "https://api.example.com/users",
            collectionId
        };

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(payload));
        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        return created!.Id;
    }

    // ──────────────────────────────────────────────
    // JavaScript Fetch
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_GenerateFetchCode_When_JavaScriptFetchLanguageRequested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=javascript-fetch");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result.Should().NotBeNull();
        result!.Language.Should().Be("javascript-fetch");
        result.RequestId.Should().Be(requestId);
        result.Code.Should().Contain("fetch");
        result.Code.Should().Contain("https://api.example.com/users");
        result.Code.Should().Contain("POST");
    }

    [Fact]
    public async Task Should_IncludeHeadersInFetchCode_When_RequestHasHeaders()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=javascript-fetch");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("Content-Type");
        result.Code.Should().Contain("Authorization");
    }

    [Fact]
    public async Task Should_IncludeBodyInFetchCode_When_RequestHasBody()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=javascript-fetch");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("body");
        result.Code.Should().Contain("testuser");
    }

    // ──────────────────────────────────────────────
    // JavaScript Axios
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_GenerateAxiosCode_When_JavaScriptAxiosLanguageRequested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=javascript-axios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result.Should().NotBeNull();
        result!.Language.Should().Be("javascript-axios");
        result.Code.Should().Contain("axios");
        result.Code.Should().Contain("https://api.example.com/users");
        result.Code.Should().Contain("post");
    }

    [Fact]
    public async Task Should_IncludeHeadersInAxiosCode_When_RequestHasHeaders()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=javascript-axios");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("Content-Type");
        result.Code.Should().Contain("Authorization");
    }

    // ──────────────────────────────────────────────
    // Python Requests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_GeneratePythonCode_When_PythonRequestsLanguageRequested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=python-requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result.Should().NotBeNull();
        result!.Language.Should().Be("python-requests");
        result.Code.Should().Contain("requests");
        result.Code.Should().Contain("https://api.example.com/users");
    }

    [Fact]
    public async Task Should_IncludeBodyInPythonCode_When_RequestHasBody()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=python-requests");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("json");
        result.Code.Should().Contain("testuser");
    }

    // ──────────────────────────────────────────────
    // C# HttpClient
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_GenerateCSharpCode_When_CSharpHttpClientLanguageRequested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=csharp-httpclient");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result.Should().NotBeNull();
        result!.Language.Should().Be("csharp-httpclient");
        result.Code.Should().Contain("HttpClient");
        result.Code.Should().Contain("https://api.example.com/users");
    }

    [Fact]
    public async Task Should_IncludeHeadersInCSharpCode_When_RequestHasHeaders()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=csharp-httpclient");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("Content-Type");
        result.Code.Should().Contain("Authorization");
    }

    // ──────────────────────────────────────────────
    // cURL
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_GenerateCurlCommand_When_CurlLanguageRequested()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=curl");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result.Should().NotBeNull();
        result!.Language.Should().Be("curl");
        result.Code.Should().Contain("curl");
        result.Code.Should().Contain("https://api.example.com/users");
        result.Code.Should().Contain("-X POST");
    }

    [Fact]
    public async Task Should_IncludeHeadersInCurl_When_RequestHasHeaders()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=curl");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("-H");
        result.Code.Should().Contain("Content-Type");
        result.Code.Should().Contain("Authorization");
    }

    [Fact]
    public async Task Should_IncludeBodyInCurl_When_RequestHasBody()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=curl");

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("-d");
        result.Code.Should().Contain("testuser");
    }

    // ──────────────────────────────────────────────
    // Generated code includes method, URL, headers, body
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_OmitBody_When_GetRequestHasNoBody()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateSimpleGetRequestAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=curl");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await TestData.Deserialize<CodeGenerationResponse>(response);
        result!.Code.Should().Contain("GET");
        result.Code.Should().NotContain("-d");
    }

    // ──────────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400_When_InvalidLanguageProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code?language=brainfuck");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_NoLanguageProvided()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var requestId = await CreateRequestWithBodyAsync(collectionId);

        var response = await _client.GetAsync(
            $"/api/requests/{requestId}/code");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_RequestDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/api/requests/{nonExistentId}/code?language=curl");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
