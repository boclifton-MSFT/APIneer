using System.Net;
using FluentAssertions;

namespace APIneer.Api.Tests.ImportExport;

/// <summary>
/// TDD Red-phase tests for cURL command import.
/// POST /api/import/curl — accepts a cURL command string and creates an APIneer request.
/// Parses method (-X), URL, headers (-H), data (-d/--data), auth (-u).
/// All tests MUST FAIL until import endpoints are implemented.
/// </summary>
public class CurlImportTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // POST /api/import/curl — Basic parsing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnOk_When_ValidCurlImported()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.SimpleCurlGet));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ParseUrl_When_SimpleCurlImported()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.SimpleCurlGet));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result.Should().NotBeNull();
        result!.Url.Should().Be("https://api.example.com/users");
    }

    [Fact]
    public async Task Should_DefaultToGet_When_NoMethodSpecified()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.SimpleCurlGet));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Method.Should().Be("GET");
    }

    // ──────────────────────────────────────────────
    // -X method flag
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ParseMethod_When_XFlagProvided()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlWithMethod));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Method.Should().Be("POST");
        result.Url.Should().Be("https://api.example.com/users");
    }

    // ──────────────────────────────────────────────
    // -H headers
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ParseHeaders_When_HFlagProvided()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlWithHeaders));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Headers.Should().NotBeNull();
        result.Headers.Should().Contain("Accept");
        result.Headers.Should().Contain("application/json");
        result.Headers.Should().Contain("Authorization");
        result.Headers.Should().Contain("Bearer token123");
    }

    // ──────────────────────────────────────────────
    // -d / --data body
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ParseBody_When_DFlagProvided()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlWithBody));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Method.Should().Be("POST");
        result.Body.Should().NotBeNull();
        result.Body.Should().Contain("username");
        result.Body.Should().Contain("test@example.com");
    }

    [Fact]
    public async Task Should_ParseBody_When_DataLongFlagProvided()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlWithDataFlag));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Method.Should().Be("PUT");
        result.Body.Should().Contain("updated");
    }

    // ──────────────────────────────────────────────
    // Multiline cURL (backslash continuation)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ParseMultilineCurl_When_BackslashContinuation()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlMultiline));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Method.Should().Be("POST");
        result.Url.Should().Be("https://api.example.com/users");
    }

    [Fact]
    public async Task Should_ParseHeadersFromMultiline_When_BackslashContinuation()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlMultiline));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Headers.Should().Contain("Content-Type");
        result.Headers.Should().Contain("Authorization");
        result.Headers.Should().Contain("Bearer mytoken");
    }

    [Fact]
    public async Task Should_ParseBodyFromMultiline_When_BackslashContinuation()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlMultiline));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Body.Should().Contain("multiline");
        result.Body.Should().Contain("multi@example.com");
    }

    // ──────────────────────────────────────────────
    // -u flag (basic auth)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_ParseBasicAuth_When_UFlagProvided()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.CurlWithBasicAuth));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.Url.Should().Be("https://api.example.com/admin/dashboard");
        // -u flag should translate to Authorization header with Basic base64
        result.Headers.Should().Contain("Authorization");
        result.Headers.Should().Contain("Basic");
    }

    [Fact]
    public async Task Should_ReturnRequestId_When_CurlImported()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(ImportExportTestData.SimpleCurlGet));

        var result = await ImportExportTestData.Deserialize<ImportExportTestData.CurlImportResultResponse>(response);
        result!.RequestId.Should().NotBeEmpty();
    }

    // ──────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400_When_EmptyCurlString()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent(""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_NotACurlCommand()
    {
        var response = await _client.PostAsync("/api/import/curl",
            ImportExportTestData.TextContent("wget https://example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
