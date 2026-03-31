using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace APIneer.Api.Tests.ImportExport;

/// <summary>
/// TDD Red-phase tests for import/export round-trip fidelity.
/// Verifies that exporting and re-importing preserves data integrity.
/// All tests MUST FAIL until import/export endpoints are implemented.
/// </summary>
public class RoundTripTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ──────────────────────────────────────────────
    // JSON round-trip: Export → Import → Verify
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PreserveStructure_When_JsonExportedThenImported()
    {
        // 1. Seed a collection with folders and requests
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        // 2. Export as JSON
        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=json");
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var exportedJson = await ImportExportTestData.ReadBody(exportResponse);

        // 3. Import the exported JSON
        var importResponse = await _client.PostAsync("/api/import/json",
            new StringContent(exportedJson, System.Text.Encoding.UTF8, "application/json"));
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult.Should().NotBeNull();
        importResult!.CollectionId.Should().NotBe(originalId); // Should be a new collection
    }

    [Fact]
    public async Task Should_PreserveCollectionName_When_JsonRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=json");
        var exportedJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/json",
            new StringContent(exportedJson, System.Text.Encoding.UTF8, "application/json"));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.CollectionName.Should().Be("Export Test Collection");
    }

    [Fact]
    public async Task Should_PreserveRequestCount_When_JsonRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=json");
        var exportedJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/json",
            new StringContent(exportedJson, System.Text.Encoding.UTF8, "application/json"));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.RequestCount.Should().Be(3); // List Users, Create User, Health Check
    }

    [Fact]
    public async Task Should_PreserveFolderCount_When_JsonRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=json");
        var exportedJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/json",
            new StringContent(exportedJson, System.Text.Encoding.UTF8, "application/json"));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.FolderCount.Should().Be(1); // Users Folder
    }

    [Fact]
    public async Task Should_PreserveRequestDetails_When_JsonRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        // Export
        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=json");
        var exportedJson = await ImportExportTestData.ReadBody(exportResponse);

        // Import
        var importResponse = await _client.PostAsync("/api/import/json",
            new StringContent(exportedJson, System.Text.Encoding.UTF8, "application/json"));
        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        // Verify imported collection has the same request details
        var getResponse = await _client.GetAsync($"/api/collections/{importResult!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        body.Should().Contain("List Users");
        body.Should().Contain("Create User");
        body.Should().Contain("Health Check");
        body.Should().Contain("https://api.example.com/users");
        body.Should().Contain("https://api.example.com/health");
    }

    // ──────────────────────────────────────────────
    // Postman round-trip: Export as Postman → Import as Postman → Verify
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PreserveStructure_When_PostmanExportedThenImported()
    {
        // 1. Seed a collection
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        // 2. Export as Postman
        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=postman");
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var postmanJson = await ImportExportTestData.ReadBody(exportResponse);

        // 3. Import the Postman export
        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = postmanJson }));
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult.Should().NotBeNull();
        importResult!.CollectionId.Should().NotBe(originalId);
    }

    [Fact]
    public async Task Should_PreserveCollectionName_When_PostmanRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=postman");
        var postmanJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = postmanJson }));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.CollectionName.Should().Be("Export Test Collection");
    }

    [Fact]
    public async Task Should_PreserveRequestCount_When_PostmanRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=postman");
        var postmanJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = postmanJson }));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.RequestCount.Should().Be(3);
    }

    [Fact]
    public async Task Should_PreserveFolderStructure_When_PostmanRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=postman");
        var postmanJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = postmanJson }));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);
        importResult!.FolderCount.Should().Be(1); // Users Folder
    }

    [Fact]
    public async Task Should_PreserveRequestMethods_When_PostmanRoundTripped()
    {
        var originalId = await ImportExportTestData.SeedCollectionWithRequestsAsync(_client);

        var exportResponse = await _client.GetAsync($"/api/collections/{originalId}/export?format=postman");
        var postmanJson = await ImportExportTestData.ReadBody(exportResponse);

        var importResponse = await _client.PostAsync("/api/import/postman",
            ImportExportTestData.JsonContent(new { collection = postmanJson }));

        var importResult = await ImportExportTestData.Deserialize<ImportExportTestData.ImportResultResponse>(importResponse);

        // Verify the imported collection has the correct methods
        var getResponse = await _client.GetAsync($"/api/collections/{importResult!.CollectionId}");
        var body = await ImportExportTestData.ReadBody(getResponse);

        body.Should().Contain("GET");
        body.Should().Contain("POST");
    }
}
