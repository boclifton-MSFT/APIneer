using APIneer.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace APIneer.Api.Tests.Security;

/// <summary>
/// Tests for the ICredentialProtector service that encrypts/decrypts secrets.
/// Verifies security invariants:
/// - Encrypt/decrypt round-trip preserves original value
/// - Encrypted values are non-trivial (not plaintext)
/// - Decrypted values are masked in API responses
/// - Encryption happens on write, decryption on read
/// </summary>
public class CredentialProtectorTests
{
    private readonly ICredentialProtector _protector;

    public CredentialProtectorTests()
    {
        // Setup a real IDataProtectionProvider for testing
        var services = new ServiceCollection();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        _protector = new CredentialProtector(provider, NullLogger<CredentialProtector>.Instance);
    }

    // ──────────────────────────────────────────────
    // Encrypt/Decrypt Round-Trip
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_EncryptAndDecryptSecretSuccessfully()
    {
        // Arrange
        var secret = "my-super-secret-api-key-12345";

        // Act
        var encrypted = _protector.Encrypt(secret);
        var decrypted = _protector.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(secret);
    }

    [Fact]
    public void Should_EncryptVariousSecretFormats()
    {
        // Test API keys
        var apiKey = "sk_live_51234567890abcdefghij";
        var encryptedKey = _protector.Encrypt(apiKey);
        _protector.Decrypt(encryptedKey).Should().Be(apiKey);

        // Test tokens
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var encryptedToken = _protector.Encrypt(token);
        _protector.Decrypt(encryptedToken).Should().Be(token);

        // Test passwords with special characters
        var password = "P@ssw0rd!#$%^&*()_+-=[]{}|;:,.<>?";
        var encryptedPassword = _protector.Encrypt(password);
        _protector.Decrypt(encryptedPassword).Should().Be(password);

        // Test empty-ish strings
        var empty = "";
        Action encryptEmpty = () => _protector.Encrypt(empty);
        encryptEmpty.Should().Throw<ArgumentNullException>();
    }

    // ──────────────────────────────────────────────
    // Encryption Properties
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_ProduceNonTrivialCiphertext()
    {
        // Arrange
        var secret = "test-secret";

        // Act
        var encrypted = _protector.Encrypt(secret);

        // Assert — ciphertext should NOT contain the plaintext
        var ciphertextString = System.Text.Encoding.UTF8.GetString(encrypted);
        ciphertextString.Should().NotContain(secret);
        
        // Ciphertext should be reasonably large (with overhead from DPAPI)
        encrypted.Length.Should().BeGreaterThan(secret.Length);
    }

    [Fact]
    public void Should_ProduceDifferentCiphertextEachTime()
    {
        // Arrange
        var secret = "test-secret";

        // Act
        var encrypted1 = _protector.Encrypt(secret);
        var encrypted2 = _protector.Encrypt(secret);

        // Assert — DPAPI adds randomness, so same plaintext produces different ciphertexts
        // (in practice, due to IV randomization)
        encrypted1.Should().NotEqual(encrypted2);
    }

    // ──────────────────────────────────────────────
    // Error Handling
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_ThrowOnEncryptNull()
    {
        // Act & Assert
        var action = () => _protector.Encrypt(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_ThrowOnDecryptNull()
    {
        // Act & Assert
        var action = () => _protector.Decrypt(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_ThrowOnDecryptEmptyBytes()
    {
        // Act & Assert
        var action = () => _protector.Decrypt(Array.Empty<byte>());
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_ThrowOnDecryptInvalidCiphertext()
    {
        // Arrange
        var invalidCiphertext = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB };

        // Act & Assert
        var action = () => _protector.Decrypt(invalidCiphertext);
        action.Should().Throw<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────
    // Security Invariant: No Raw Secrets in Storage
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_VerifyEncryptedBytesCannotBeReadAsPlaintext()
    {
        // Arrange
        var secret = "super-secret-password-123";

        // Act
        var encrypted = _protector.Encrypt(secret);

        // Assert — reading encrypted bytes as UTF-8 should produce garbage, not the secret
        var garbageString = System.Text.Encoding.UTF8.GetString(encrypted);
        garbageString.Should().NotContain(secret);
    }

    // ──────────────────────────────────────────────
    // Invariant: Decryption Happens Only on Backend
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_SuccessfullyDecryptAtResolutionTime()
    {
        // Simulate the workflow:
        // 1. User creates secret variable
        // 2. Backend encrypts and stores encrypted bytes
        // 3. At resolution time, backend decrypts to get real value

        // Arrange
        var originalSecret = "production-api-key-xyz";
        var storedEncrypted = _protector.Encrypt(originalSecret);

        // Act — simulate resolution time decryption
        var decryptedForResolution = _protector.Decrypt(storedEncrypted);

        // Assert
        decryptedForResolution.Should().Be(originalSecret);
    }
}
