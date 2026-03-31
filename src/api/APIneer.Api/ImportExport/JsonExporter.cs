using APIneer.Api.Models;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Exports a collection as APIneer native JSON format.
/// </summary>
public static class JsonExporter
{
    public static object Export(Collection collection)
    {
        var allFolders = collection.Folders.ToList();
        var allRequests = collection.Requests.ToList();

        var rootFolders = allFolders
            .Where(f => f.ParentFolderId == null)
            .OrderBy(f => f.SortOrder)
            .Select(f => MapFolder(f, allFolders, allRequests))
            .ToArray();

        var rootRequests = allRequests
            .Where(r => r.FolderId == null)
            .OrderBy(r => r.SortOrder)
            .Select(MapRequest)
            .ToArray();

        return new
        {
            collection.Id,
            collection.Name,
            collection.Description,
            Folders = rootFolders,
            Requests = rootRequests
        };
    }

    private static object MapFolder(CollectionFolder folder, List<CollectionFolder> allFolders, List<ApiRequest> allRequests)
    {
        var subFolders = allFolders
            .Where(f => f.ParentFolderId == folder.Id)
            .OrderBy(f => f.SortOrder)
            .Select(f => MapFolder(f, allFolders, allRequests))
            .ToArray();

        var requests = allRequests
            .Where(r => r.FolderId == folder.Id)
            .OrderBy(r => r.SortOrder)
            .Select(MapRequest)
            .ToArray();

        return new
        {
            folder.Name,
            SubFolders = subFolders,
            Requests = requests
        };
    }

    private static object MapRequest(ApiRequest r) => new
    {
        r.Name,
        r.Method,
        r.Url,
        r.Headers,
        r.Body,
        r.BodyType
    };
}
