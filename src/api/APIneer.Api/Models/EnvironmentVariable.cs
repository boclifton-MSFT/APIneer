namespace APIneer.Api.Models;

/// <summary>
/// A key-value variable within an environment.
/// Secret values are stored as encrypted bytes; plaintext values are stored as-is.
/// </summary>
public class EnvironmentVariable
{
    public Guid Id { get; set; }
    public Guid EnvironmentId { get; set; }
    public required string Key { get; set; }
    
    /// <summary>
    /// Plaintext value for non-secret variables, or encrypted bytes for secrets.
    /// When IsSecret=true, this contains the encrypted ciphertext as a base64-encoded string.
    /// </summary>
    public required string Value { get; set; }
    
    public bool IsSecret { get; set; }
    public DateTime CreatedAt { get; set; }

    public Environment Environment { get; set; } = null!;
}
