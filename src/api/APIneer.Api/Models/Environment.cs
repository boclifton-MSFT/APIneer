namespace APIneer.Api.Models;

/// <summary>
/// A named environment containing variable sets for a workspace.
/// </summary>
public class Environment
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<EnvironmentVariable> Variables { get; set; } = [];
}
