using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace APIneer.Api.Services;

/// <summary>
/// Encrypts and decrypts secret values using .NET Data Protection API (DPAPI).
/// Secrets are encrypted when stored and decrypted only at request execution time.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>
    /// Encrypts a plaintext secret value.
    /// </summary>
    /// <param name="plaintext">The secret value to encrypt</param>
    /// <returns>Encrypted bytes that can be safely stored in the database</returns>
    byte[] Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a previously encrypted secret value.
    /// </summary>
    /// <param name="ciphertext">The encrypted bytes from the database</param>
    /// <returns>The decrypted plaintext secret</returns>
    /// <exception cref="InvalidOperationException">If decryption fails</exception>
    string Decrypt(byte[] ciphertext);
}

/// <summary>
/// Implementation using .NET DPAPI for platform-specific encryption.
/// </summary>
public class CredentialProtector(IDataProtectionProvider provider, ILogger<CredentialProtector> logger) : ICredentialProtector
{
    private readonly IDataProtector _protector = (provider ?? throw new ArgumentNullException(nameof(provider)))
        .CreateProtector("APIneer.CredentialProtection");
    private readonly ILogger<CredentialProtector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public byte[] Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentNullException(nameof(plaintext));

        try
        {
            var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var encrypted = _protector.Protect(plaintextBytes);
            _logger.LogDebug("Credential encryption successful");
            return encrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt credential");
            throw new InvalidOperationException("Credential encryption failed", ex);
        }
    }

    public string Decrypt(byte[] ciphertext)
    {
        if (ciphertext == null || ciphertext.Length == 0)
            throw new ArgumentNullException(nameof(ciphertext));

        try
        {
            var decrypted = _protector.Unprotect(ciphertext);
            var plaintext = System.Text.Encoding.UTF8.GetString(decrypted);
            _logger.LogDebug("Credential decrypted for resolution");
            return plaintext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credential");
            throw new InvalidOperationException("Credential decryption failed", ex);
        }
    }
}
