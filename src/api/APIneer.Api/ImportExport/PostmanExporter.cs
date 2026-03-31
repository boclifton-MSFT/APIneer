using System.Text.Json;
using APIneer.Api.Models;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Exports a collection as Postman v2.1 format.
/// </summary>
public static class PostmanExporter
{
    private const string PostmanSchema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json";

    public static object Export(Collection collection)
    {
        var allFolders = collection.Folders.ToList();
        var allRequests = collection.Requests.ToList();

        var rootItems = new List<object>();

        // Add folder items (with nested requests)
        var rootFolders = allFolders
            .Where(f => f.ParentFolderId == null)
            .OrderBy(f => f.SortOrder);

        foreach (var folder in rootFolders)
        {
            rootItems.Add(MapFolderToItem(folder, allFolders, allRequests));
        }

        // Add root-level request items
        var rootRequests = allRequests
            .Where(r => r.FolderId == null)
            .OrderBy(r => r.SortOrder);

        foreach (var request in rootRequests)
        {
            rootItems.Add(MapRequestToItem(request));
        }

        return new
        {
            Info = new
            {
                Name = collection.Name,
                Description = collection.Description,
                Schema = PostmanSchema
            },
            Item = rootItems.ToArray()
        };
    }

    private static object MapFolderToItem(CollectionFolder folder, List<CollectionFolder> allFolders, List<ApiRequest> allRequests)
    {
        var childItems = new List<object>();

        // Sub-folders
        var subFolders = allFolders
            .Where(f => f.ParentFolderId == folder.Id)
            .OrderBy(f => f.SortOrder);

        foreach (var subFolder in subFolders)
        {
            childItems.Add(MapFolderToItem(subFolder, allFolders, allRequests));
        }

        // Folder requests
        var folderRequests = allRequests
            .Where(r => r.FolderId == folder.Id)
            .OrderBy(r => r.SortOrder);

        foreach (var request in folderRequests)
        {
            childItems.Add(MapRequestToItem(request));
        }

        return new
        {
            folder.Name,
            Item = childItems.ToArray()
        };
    }

    private static object MapRequestToItem(ApiRequest request)
    {
        var headers = ParseHeaders(request.Headers);
        object? body = null;
        if (!string.IsNullOrEmpty(request.Body))
        {
            body = new
            {
                Mode = "raw",
                Raw = request.Body
            };
        }

        return new
        {
            request.Name,
            Request = new
            {
                request.Method,
                Header = headers,
                Body = body,
                Url = new
                {
                    Raw = request.Url
                }
            }
        };
    }

    private static object[]? ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrEmpty(headersJson)) return null;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            if (dict is null || dict.Count == 0) return null;

            return dict.Select(kvp => (object)new { Key = kvp.Key, Value = kvp.Value }).ToArray();
        }
        catch
        {
            return null;
        }
    }
}
