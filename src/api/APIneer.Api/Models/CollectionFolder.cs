namespace APIneer.Api.Models;

/// <summary>
/// A folder within a collection, supporting nested hierarchy.
/// </summary>
public class CollectionFolder
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Guid? ParentFolderId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public Collection Collection { get; set; } = null!;
    public CollectionFolder? ParentFolder { get; set; }
    public ICollection<CollectionFolder> SubFolders { get; set; } = [];
    public ICollection<ApiRequest> Requests { get; set; } = [];
}
