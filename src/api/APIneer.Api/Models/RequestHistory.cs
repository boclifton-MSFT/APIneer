namespace APIneer.Api.Models;

/// <summary>
/// A recorded execution of an API request with full request/response details.
/// </summary>
public class RequestHistory
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public required string Method { get; set; }
    public required string Url { get; set; }
    public string? RequestHeaders { get; set; }
    public string? RequestBody { get; set; }
    public int ResponseStatus { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }
    public long ResponseTimeMs { get; set; }
    public long ResponseSizeBytes { get; set; }
    public DateTime ExecutedAt { get; set; }

    public ApiRequest Request { get; set; } = null!;
}
