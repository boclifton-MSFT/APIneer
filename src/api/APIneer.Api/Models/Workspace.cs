namespace APIneer.Api.Models;

/// <summary>
/// Represents a workspace that organizes collections and environments.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Collection> Collections { get; set; } = [];
    public ICollection<Environment> Environments { get; set; } = [];
}
