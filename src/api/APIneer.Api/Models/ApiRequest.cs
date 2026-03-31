namespace APIneer.Api.Models;

/// <summary>
/// An API request definition stored within a collection.
/// </summary>
public class ApiRequest
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Guid? FolderId { get; set; }
    public required string Name { get; set; }
    public required string Method { get; set; }
    public required string Url { get; set; }
    public string? Headers { get; set; }
    public string? Body { get; set; }
    public string? BodyType { get; set; }
    public string? AuthConfig { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Collection Collection { get; set; } = null!;
    public CollectionFolder? Folder { get; set; }
    public ICollection<RequestHistory> History { get; set; } = [];
    public ICollection<Assertion> Assertions { get; set; } = [];
}
