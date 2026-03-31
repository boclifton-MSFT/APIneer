using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.ImportExport;

/// <summary>
/// TDD Red-phase tests for collection export endpoints.
/// GET /api/collections/{id}/export?format=json|curl|postman
/// All tests MUST FAIL until export endpoints are implemented.
/// </summary>
public class ExportTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // Export as JSON (APIneer native format)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnOk_When_ExportingAsJson()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnCollectionName_When_ExportingAsJson()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=json");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportJsonResponse>(response);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Export Test Collection");
        result.Description.Should().Be("For export tests");
    }

    [Fact]
    public async Task Should_IncludeFolders_When_ExportingAsJson()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=json");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportJsonResponse>(response);
        result!.Folders.Should().NotBeNullOrEmpty();
        result.Folders!.Should().Contain(f => f.Name == "Users Folder");
    }

    [Fact]
    public async Task Should_IncludeRequests_When_ExportingAsJson()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=json");

        var body = await ImportExportTestData.ReadBody(response);
        body.Should().Contain("List Users");
        body.Should().Contain("Create User");
        body.Should().Contain("Health Check");
    }

    [Fact]
    public async Task Should_IncludeRequestDetails_When_ExportingAsJson()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=json");

        var body = await ImportExportTestData.ReadBody(response);
        body.Should().Contain("https://api.example.com/users");
        body.Should().Contain("Bearer token123");
    }

    // ──────────────────────────────────────────────
    // Export as cURL
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnOk_When_ExportingAsCurl()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=curl");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnCurlCommands_When_ExportingAsCurl()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=curl");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportCurlCollectionResponse>(response);
        result.Should().NotBeNull();
        result!.Requests.Should().NotBeEmpty();
        result.Requests.Length.Should().Be(3);
    }

    [Fact]
    public async Task Should_GenerateValidCurlSyntax_When_ExportingGetRequest()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=curl");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportCurlCollectionResponse>(response);
        var healthCheck = result!.Requests.FirstOrDefault(r => r.RequestName == "Health Check");
        healthCheck.Should().NotBeNull();
        healthCheck!.CurlCommand.Should().StartWith("curl");
        healthCheck.CurlCommand.Should().Contain("https://api.example.com/health");
    }

    [Fact]
    public async Task Should_IncludeHeaders_When_ExportingAsCurl()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=curl");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportCurlCollectionResponse>(response);
        var listUsers = result!.Requests.FirstOrDefault(r => r.RequestName == "List Users");
        listUsers.Should().NotBeNull();
        listUsers!.CurlCommand.Should().Contain("-H");
        listUsers.CurlCommand.Should().Contain("Authorization: Bearer token123");
    }

    [Fact]
    public async Task Should_IncludeBody_When_ExportingPostRequestAsCurl()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=curl");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.ExportCurlCollectionResponse>(response);
        var createUser = result!.Requests.FirstOrDefault(r => r.RequestName == "Create User");
        createUser.Should().NotBeNull();
        createUser!.CurlCommand.Should().Contain("-X POST");
        createUser.CurlCommand.Should().Contain("-d");
        createUser.CurlCommand.Should().Contain("newuser");
    }

    // ──────────────────────────────────────────────
    // Export as Postman v2.1
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnOk_When_ExportingAsPostman()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=postman");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnPostmanSchema_When_ExportingAsPostman()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=postman");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.PostmanExportResponse>(response);
        result.Should().NotBeNull();
        result!.Info.Schema.Should().Be("https://schema.getpostman.com/json/collection/v2.1.0/collection.json");
    }

    [Fact]
    public async Task Should_IncludeCollectionName_When_ExportingAsPostman()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=postman");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.PostmanExportResponse>(response);
        result!.Info.Name.Should().Be("Export Test Collection");
    }

    [Fact]
    public async Task Should_IncludeItems_When_ExportingAsPostman()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=postman");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.PostmanExportResponse>(response);
        result!.Item.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_RepresentFoldersAsNestedItems_When_ExportingAsPostman()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=postman");

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.PostmanExportResponse>(response);
        // Should have a folder item with nested request items
        var folder = result!.Item.FirstOrDefault(i => i.Name == "Users Folder");
        folder.Should().NotBeNull();
        folder!.Item.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400_When_InvalidExportFormat()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export?format=xml");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_MissingFormatParameter()
    {
        var collectionId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var response = await _client.GetAsync($"/api/collections/{collectionId}/export");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_ExportingNonExistentCollection()
    {
        var fakeId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/collections/{fakeId}/export?format=json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
