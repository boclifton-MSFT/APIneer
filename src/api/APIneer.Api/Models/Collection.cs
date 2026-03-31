namespace APIneer.Api.Models;

/// <summary>
/// A collection of API requests, organized within a workspace.
/// </summary>
public class Collection
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<CollectionFolder> Folders { get; set; } = [];
    public ICollection<ApiRequest> Requests { get; set; } = [];
}
