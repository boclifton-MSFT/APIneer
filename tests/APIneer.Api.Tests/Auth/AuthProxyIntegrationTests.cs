using System.Net;
using System.Text;
using System.Text.Json;
using APIneer.Api.Tests.Requests;
using FluentAssertions;

namespace APIneer.Api.Tests.Auth;

/// <summary>
/// Integration tests verifying auth config flows end-to-end through the proxy/send endpoint.
/// Tests: CRUD round-trip of auth config, auth application on send, error handling.
/// </summary>
public class AuthProxyIntegrationTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ──────────────────────────────────────────────
    // Auth config round-trip: save → load → preserved
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_PersistAuthConfig_When_CreatingRequestWithBearerAuth()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);
        var authConfig = JsonSerializer.Serialize(new { type = "bearer", token = "my-jwt-token" });

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "Auth Test",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created!.AuthConfig.Should().NotBeNullOrEmpty();

        // Reload via GET
        var getResponse = await _client.GetAsync($"/api/requests/{created.Id}");
        var fetched = await TestData.Deserialize<TestData.RequestResponse>(getResponse);
        fetched!.AuthConfig.Should().Be(authConfig);
    }

    [Fact]
    public async Task Should_UpdateAuthConfig_When_PuttingRequestWithNewAuth()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        // Create with bearer auth
        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "Auth Update Test",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "bearer", token = "old-token" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Update to API key auth
        var newAuth = JsonSerializer.Serialize(new { type = "api_key", keyName = "X-Api-Key", keyValue = "secret-123", placement = "header" });
        var updateResponse = await _client.PutAsync($"/api/requests/{created!.Id}",
            TestData.JsonContent(new
            {
                name = "Auth Update Test",
                method = "GET",
                url = "https://api.example.com/data",
                authConfig = newAuth
            }));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await TestData.Deserialize<TestData.RequestResponse>(updateResponse);
        updated!.AuthConfig.Should().Be(newAuth);
    }

    [Fact]
    public async Task Should_ReturnNullAuthConfig_When_CreatingRequestWithoutAuth()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var response = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await TestData.Deserialize<TestData.RequestResponse>(response);
        created!.AuthConfig.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Send endpoint applies auth — error cases
    // (We can't easily test successful header injection via HTTP integration
    //  since the target URL is unreachable, but we CAN test error handling)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400_When_SendingRequestWithUnsupportedAuthType()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "Bad Auth",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "custom_unsupported" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task Should_Return400_When_BearerAuthMissingToken()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "No Token",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "bearer" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await sendResponse.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task Should_Return400_When_BasicAuthMissingCredentials()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "No Creds",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "basic" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return400_When_ApiKeyMissingKeyName()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "No Key Name",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "api_key", keyValue = "val" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_SendSuccessfully_When_AuthTypeIsNone()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "No Auth",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new { type = "none" })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // Should not return 400 — "none" is valid, just does nothing
        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_SendSuccessfully_When_NoAuthConfigSet()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(TestData.ValidGetRequest(collectionId)));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        // No auth config → no auth applied → should proceed to proxy (may get network error, but not 400)
        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Return502_When_OAuth2TokenEndpointFails()
    {
        var collectionId = await TestData.SeedCollectionAsync(_client);

        var createResponse = await _client.PostAsync("/api/requests",
            TestData.JsonContent(new
            {
                name = "OAuth Fail",
                method = "GET",
                url = "https://api.example.com/data",
                collectionId,
                authConfig = JsonSerializer.Serialize(new
                {
                    type = "oauth2",
                    tokenEndpoint = "https://invalid-oauth.example.com/token",
                    clientId = "my-client",
                    clientSecret = "my-secret"
                })
            }));
        var created = await TestData.Deserialize<TestData.RequestResponse>(createResponse);

        var sendResponse = await _client.PostAsync($"/api/requests/{created!.Id}/send", null);
        // OAuth2 failure should return 502 (upstream error) or 400 depending on the error type
        var status = (int)sendResponse.StatusCode;
        status.Should().BeOneOf(400, 502);
    }
}
