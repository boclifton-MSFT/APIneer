using System.Text.Json;
using APIneer.Api.Data;
using APIneer.Api.Models;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Parses Postman v2.1 JSON collections and creates APIneer collections
/// with matching folder/request structure.
/// </summary>
public static class PostmanImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ImportResult> ImportAsync(AppDbContext db, string postmanJson)
    {
        PostmanCollection parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PostmanCollection>(postmanJson, JsonOptions)
                ?? throw new InvalidOperationException("Parsed result was null.");
        }
        catch
        {
            throw new ImportValidationException("Invalid Postman JSON format.");
        }

        if (parsed.Info?.Schema is null ||
            !parsed.Info.Schema.Contains("collection/v2", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImportValidationException("Missing or invalid Postman schema field.");
        }

        var collectionName = parsed.Info.Name ?? "Imported Collection";
        var description = parsed.Info.Description;

        // Create a default workspace for imports
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Imported Workspace",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Workspaces.Add(workspace);

        var now = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = collectionName,
            Description = description,
            WorkspaceId = workspace.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Collections.Add(collection);

        int requestCount = 0;
        int folderCount = 0;

        if (parsed.Item != null)
        {
            ProcessItems(db, parsed.Item, collection.Id, null, ref requestCount, ref folderCount);
        }

        await db.SaveChangesAsync();

        return new ImportResult(collection.Id, collectionName, requestCount, folderCount);
    }

    private static void ProcessItems(
        AppDbContext db,
        PostmanItem[] items,
        Guid collectionId,
        Guid? parentFolderId,
        ref int requestCount,
        ref int folderCount)
    {
        int sortOrder = 0;
        foreach (var item in items)
        {
            if (item.Item != null && item.Item.Length > 0)
            {
                // This is a folder
                var folder = new CollectionFolder
                {
                    Id = Guid.NewGuid(),
                    CollectionId = collectionId,
                    ParentFolderId = parentFolderId,
                    Name = item.Name ?? "Unnamed Folder",
                    SortOrder = sortOrder++
                };
                db.CollectionFolders.Add(folder);
                folderCount++;

                // If the folder also has a request, add it
                if (item.Request != null)
                {
                    CreateRequest(db, item, collectionId, folder.Id, ref requestCount, ref sortOrder);
                }

                ProcessItems(db, item.Item, collectionId, folder.Id, ref requestCount, ref folderCount);
            }
            else if (item.Request != null)
            {
                CreateRequest(db, item, collectionId, parentFolderId, ref requestCount, ref sortOrder);
            }
        }
    }

    private static void CreateRequest(
        AppDbContext db,
        PostmanItem item,
        Guid collectionId,
        Guid? folderId,
        ref int requestCount,
        ref int sortOrder)
    {
        var req = item.Request!;
        var method = req.Method?.ToUpperInvariant() ?? "GET";
        var url = ExtractUrl(req.Url);
        var headers = ExtractHeaders(req.Header);
        var body = req.Body?.Raw;
        var bodyType = DetectBodyType(req.Body);

        var apiRequest = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collectionId,
            FolderId = folderId,
            Name = item.Name ?? "Imported Request",
            Method = method,
            Url = url,
            Headers = headers,
            Body = body,
            BodyType = bodyType,
            SortOrder = sortOrder++,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ApiRequests.Add(apiRequest);
        requestCount++;
    }

    private static string ExtractUrl(PostmanUrl? url)
    {
        if (url is null) return "";
        return url.Raw ?? "";
    }

    private static string? ExtractHeaders(PostmanHeader[]? headers)
    {
        if (headers is null || headers.Length == 0) return null;

        var dict = new Dictionary<string, string>();
        foreach (var h in headers)
        {
            if (!string.IsNullOrEmpty(h.Key))
                dict[h.Key] = h.Value ?? "";
        }

        return dict.Count > 0
            ? JsonSerializer.Serialize(dict)
            : null;
    }

    private static string? DetectBodyType(PostmanBody? body)
    {
        if (body is null || string.IsNullOrEmpty(body.Raw)) return null;

        if (body.Options?.Raw?.Language is not null)
        {
            return body.Options.Raw.Language.ToLowerInvariant() switch
            {
                "json" => "application/json",
                "xml" => "application/xml",
                "text" => "text/plain",
                _ => "text/plain"
            };
        }

        return "text/plain";
    }

    // ──────────────────────────────────────────────
    // Postman v2.1 JSON model
    // ──────────────────────────────────────────────

    private record PostmanCollection(PostmanInfo? Info, PostmanItem[]? Item);
    private record PostmanInfo(string? Name, string? Description, string? Schema);
    private record PostmanItem(string? Name, PostmanRequest? Request, PostmanItem[]? Item);
    private record PostmanRequest(string? Method, PostmanHeader[]? Header, PostmanBody? Body, PostmanUrl? Url);
    private record PostmanHeader(string? Key, string? Value);
    private record PostmanBody(string? Mode, string? Raw, PostmanBodyOptions? Options);
    private record PostmanBodyOptions(PostmanRawOptions? Raw);
    private record PostmanRawOptions(string? Language);
    private record PostmanUrl(string? Raw, string? Protocol, string[]? Host, string[]? Path, PostmanQuery[]? Query);
    private record PostmanQuery(string? Key, string? Value);
}

public record ImportResult(Guid CollectionId, string CollectionName, int RequestCount, int FolderCount);

public class ImportValidationException(string message) : Exception(message);
