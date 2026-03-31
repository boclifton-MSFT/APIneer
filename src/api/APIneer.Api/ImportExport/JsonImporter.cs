using System.Text.Json;
using APIneer.Api.Data;
using APIneer.Api.Models;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Imports an APIneer native JSON export back into the database,
/// creating a new collection with new IDs.
/// </summary>
public static class JsonImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ImportResult> ImportAsync(AppDbContext db, string json)
    {
        JsonExportModel parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<JsonExportModel>(json, JsonOptions)
                ?? throw new InvalidOperationException("Parsed result was null.");
        }
        catch (Exception ex) when (ex is not ImportValidationException)
        {
            throw new ImportValidationException("Invalid APIneer JSON format.");
        }

        if (string.IsNullOrWhiteSpace(parsed.Name))
            throw new ImportValidationException("Collection name is required.");

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
            Name = parsed.Name,
            Description = parsed.Description,
            WorkspaceId = workspace.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Collections.Add(collection);

        int requestCount = 0;
        int folderCount = 0;

        // Process folders
        if (parsed.Folders != null)
        {
            foreach (var folder in parsed.Folders)
            {
                ProcessFolder(db, folder, collection.Id, null, ref requestCount, ref folderCount);
            }
        }

        // Process root requests
        if (parsed.Requests != null)
        {
            int sortOrder = 0;
            foreach (var req in parsed.Requests)
            {
                CreateRequest(db, req, collection.Id, null, ref requestCount, ref sortOrder);
            }
        }

        await db.SaveChangesAsync();

        return new ImportResult(collection.Id, parsed.Name, requestCount, folderCount);
    }

    private static void ProcessFolder(
        AppDbContext db,
        JsonFolderModel folder,
        Guid collectionId,
        Guid? parentFolderId,
        ref int requestCount,
        ref int folderCount)
    {
        var dbFolder = new CollectionFolder
        {
            Id = Guid.NewGuid(),
            CollectionId = collectionId,
            ParentFolderId = parentFolderId,
            Name = folder.Name ?? "Unnamed Folder",
            SortOrder = folderCount
        };
        db.CollectionFolders.Add(dbFolder);
        folderCount++;

        // Process sub-folders
        if (folder.SubFolders != null)
        {
            foreach (var sub in folder.SubFolders)
            {
                ProcessFolder(db, sub, collectionId, dbFolder.Id, ref requestCount, ref folderCount);
            }
        }

        // Process folder requests
        if (folder.Requests != null)
        {
            int sortOrder = 0;
            foreach (var req in folder.Requests)
            {
                CreateRequest(db, req, collectionId, dbFolder.Id, ref requestCount, ref sortOrder);
            }
        }
    }

    private static void CreateRequest(
        AppDbContext db,
        JsonRequestModel req,
        Guid collectionId,
        Guid? folderId,
        ref int requestCount,
        ref int sortOrder)
    {
        var apiRequest = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collectionId,
            FolderId = folderId,
            Name = req.Name ?? "Imported Request",
            Method = req.Method ?? "GET",
            Url = req.Url ?? "",
            Headers = req.Headers,
            Body = req.Body,
            BodyType = req.BodyType,
            SortOrder = sortOrder++,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ApiRequests.Add(apiRequest);
        requestCount++;
    }

    // ──────────────────────────────────────────────
    // JSON import models
    // ──────────────────────────────────────────────

    private record JsonExportModel(
        Guid? Id,
        string? Name,
        string? Description,
        JsonFolderModel[]? Folders,
        JsonRequestModel[]? Requests);

    private record JsonFolderModel(
        string? Name,
        JsonFolderModel[]? SubFolders,
        JsonRequestModel[]? Requests);

    private record JsonRequestModel(
        string? Name,
        string? Method,
        string? Url,
        string? Headers,
        string? Body,
        string? BodyType);
}
