using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.ImportExport;

/// <summary>
/// TDD Red-phase tests for Postman v2.1 collection import.
/// POST /api/import/postman — accepts Postman v2.1 JSON collection and creates
/// an APIneer collection with matching structure (folders, requests, headers, body).
/// All tests MUST FAIL until import endpoints are implemented.
/// </summary>
public class PostmanImportTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/import/postman — Basic import
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnOk_When_ValidPostmanCollectionImported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnCollectionId_When_PostmanCollectionImported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(response);
        result.Should().NotBeNull();
        result!.CollectionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_UsePostmanCollectionName_When_Imported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(response);
        result!.CollectionName.Should().Be("Sample API Collection");
    }

    [Fact]
    public async Task Should_CreateCorrectRequestCount_When_PostmanCollectionImported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(response);
        // 4 requests: Get All Users, Create User, Login, Health Check
        result!.RequestCount.Should().Be(4);
    }

    [Fact]
    public async Task Should_CreateCorrectFolderCount_When_PostmanCollectionImported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(response);
        // 2 folders: Users, Auth (nested under Users)
        result!.FolderCount.Should().Be(2);
    }

    // ──────────────────────────────────────────────
    // Verify imported structure via GET
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PreserveRequestMethod_When_PostmanImported()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        // Fetch the collection to verify structure
        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ImportExportTestData.ReadBody(getResponse);
        // Should contain both GET and POST requests
        body.Should().Contain("GET");
        body.Should().Contain("POST");
    }

    [Fact]
    public async Task Should_PreserveRequestUrl_When_PostmanImported()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        // Should preserve URL with query params
        body.Should().Contain("https://api.example.com/users?page=1&limit=10");
        body.Should().Contain("https://api.example.com/health");
    }

    [Fact]
    public async Task Should_PreserveHeaders_When_PostmanImported()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        body.Should().Contain("Accept");
        body.Should().Contain("application/json");
        body.Should().Contain("Authorization");
    }

    [Fact]
    public async Task Should_PreserveRequestBody_When_PostmanImported()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        body.Should().Contain("newuser");
        body.Should().Contain("new@example.com");
    }

    [Fact]
    public async Task Should_CreateFolderStructure_When_PostmanImported()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21Collection }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        // Should have "Users" folder and "Auth" subfolder
        body.Should().Contain("Users");
        body.Should().Contain("Auth");
    }

    // ──────────────────────────────────────────────
    // Nested folders
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_HandleDeeplyNestedFolders_When_PostmanImported()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21CollectionNested }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(response);
        result!.RequestCount.Should().Be(1); // Deep Request
        result.FolderCount.Should().Be(3);   // Level 1, Level 2, Level 3
    }

    [Fact]
    public async Task Should_PreserveNestedRequestMethod_When_DeeplyNested()
    {
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = ImportExportTestData.PostmanV21CollectionNested }));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        var getResponse = await _client.GetAsync($"/api/collections/{result!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        body.Should().Contain("DELETE");
        body.Should().Contain("https://api.example.com/deep/resource/123");
    }

    // ──────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400_When_InvalidPostmanJson()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = "not valid json at all {{{" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_EmptyCollectionBody()
    {
        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = "" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_MissingSchemaField()
    {
        var noSchema = """
        {
          "info": { "name": "No Schema" },
          "item": []
        }
        """;

        var response = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = noSchema }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
