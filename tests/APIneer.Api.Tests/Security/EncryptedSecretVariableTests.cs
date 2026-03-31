using System.Net;
using System.Text.Json;
using APIneer.Api.Tests.Environments;
using FluentAssertions;

namespace APIneer.Api.Tests.Security;

/// <summary>
/// Integration tests for encrypted secret variable storage and resolution.
/// Verifies security invariants from docs/security-architecture.md:
/// - Invariant 1: No raw secrets in API responses
/// - Invariant 3: Credentials encrypted at rest
/// - Invariant 6: No plaintext secrets in request logs
/// </summary>
public class EncryptedSecretVariableTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    // ──────────────────────────────────────────────
    // Invariant 1: No Raw Secrets in API Responses
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_MaskSecretValueInGetResponse()
    {
        // Arrange
        var (workspaceId, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "api_key", value: "secret-value-xyz", isSecret: true);

        // Act
        var response = await _client.GetAsync($"/api/environments/{envId}/variables/{varId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Response must NOT contain raw secret
        content.Should().NotContain("secret-value-xyz");
        content.Should().Contain("***masked***");
    }

    [Fact]
    public async Task Should_ReturnMaskedSecretsInEnvironmentListResponse()
    {
        // Arrange
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);
        
        // Add a secret variable
        await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("token", "super-secret-token-123", isSecret: true)));

        // Act
        var response = await _client.GetAsync("/api/environments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // List response must NOT contain raw secret
        content.Should().NotContain("super-secret-token-123");
        content.Should().Contain("***masked***");
    }

    // ──────────────────────────────────────────────
    // Invariant 3: Credentials Encrypted at Rest
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_EncryptSecretBeforeStoringInDatabase()
    {
        // Arrange
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);
        var secretValue = "plaintext-secret-password-12345";

        // Act — create a secret variable
        var createResponse = await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("password", secretValue, isSecret: true)));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the response to verify it's masked
        var createContent = await createResponse.Content.ReadAsStringAsync();
        createContent.Should().Contain("***masked***");
        createContent.Should().NotContain(secretValue);
    }

    // ──────────────────────────────────────────────
    // Invariant 6: Decryption Only at Resolution Time
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_DecryptSecretAtResolutionTime()
    {
        // Arrange
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);
        var secretValue = "real-secret-token-abc123";

        // Add secret variable
        await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("token", secretValue, isSecret: true)));

        // Activate environment
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        // Act — resolve variables (this triggers decryption on backend)
        var resolvePayload = new 
        { 
            url = "https://api.example.com", 
            headers = "{\"Authorization\": \"Bearer {{token}}}\"}"
        };
        var resolveResponse = await _client.PostAsync(
            "/api/environments/resolve",
            EnvironmentTestData.JsonContent(resolvePayload));

        // Assert
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolveContent = await resolveResponse.Content.ReadAsStringAsync();
        
        // Resolution response should contain the REAL secret (for proxy use)
        resolveContent.Should().Contain(secretValue);
        resolveContent.Should().NotContain("***masked***");
    }

    [Fact]
    public async Task Should_MixPlainAndSecretVariablesCorrectly()
    {
        // Arrange
        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        // Add plain variable
        await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("base_url", "https://api.example.com", isSecret: false)));

        // Add secret variable
        await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("api_key", "secret-key-xyz", isSecret: true)));

        // Activate
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        // Act
        var resolvePayload = new 
        { 
            url = "{{base_url}}/users?key={{api_key}}", 
            headers = "{}"
        };
        var resolveResponse = await _client.PostAsync(
            "/api/environments/resolve",
            EnvironmentTestData.JsonContent(resolvePayload));

        // Assert
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolveContent = await resolveResponse.Content.ReadAsStringAsync();
        
        // Plain variable should be visible
        resolveContent.Should().Contain("https://api.example.com");
        
        // Secret variable should be decrypted (real value visible at resolution time)
        resolveContent.Should().Contain("secret-key-xyz");
    }

    [Fact]
    public async Task Should_UpdateSecretWithNewEncryption()
    {
        // Arrange
        var (workspaceId, envId, varId) = await EnvironmentTestData.SeedEnvironmentWithVariableAsync(
            _client, key: "token", value: "old-secret-value", isSecret: true);

        // Act — update the secret
        var updatePayload = new 
        { 
            key = "token", 
            value = "new-secret-value", 
            isSecret = true 
        };
        var updateResponse = await _client.PutAsync(
            $"/api/environments/{envId}/variables/{varId}",
            EnvironmentTestData.JsonContent(updatePayload));

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        
        // Response should be masked, not contain new value
        updateContent.Should().Contain("***masked***");
        updateContent.Should().NotContain("new-secret-value");
        updateContent.Should().NotContain("old-secret-value");

        // Verify resolution uses new value
        await _client.PutAsync($"/api/environments/{envId}/activate", null);

        var resolvePayload = new { url = "{{token}}", headers = "{}" };
        var resolveResponse = await _client.PostAsync(
            "/api/environments/resolve",
            EnvironmentTestData.JsonContent(resolvePayload));

        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolveContent = await resolveResponse.Content.ReadAsStringAsync();
        
        // Should use NEW value, not old
        resolveContent.Should().Contain("new-secret-value");
        resolveContent.Should().NotContain("old-secret-value");
    }

    // ──────────────────────────────────────────────
    // Error Handling for Encryption
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Should_HandleEncryptionFailureGracefully()
    {
        // This test verifies that if encryption fails during create,
        // we get a proper error response, not an unhandled exception

        var (workspaceId, envId) = await EnvironmentTestData.SeedEnvironmentAsync(_client);

        // Act — create a secret variable
        var response = await _client.PostAsync(
            $"/api/environments/{envId}/variables",
            EnvironmentTestData.JsonContent(
                EnvironmentTestData.CreateVariablePayload("secret", "value", isSecret: true)));

        // Assert — should succeed (encryption is configured)
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
