namespace APIneer.Api.Models;

/// <summary>
/// A test assertion associated with an API request.
/// Evaluated when the request is executed via the /test endpoint.
/// </summary>
public class Assertion
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public required string Type { get; set; }
    public required string Expected { get; set; }
    public DateTime CreatedAt { get; set; }

    public ApiRequest Request { get; set; } = null!;
}
