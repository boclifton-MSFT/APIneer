using APIneer.Api.Auth;
using APIneer.Api.Data;
using APIneer.Api.ImportExport;
using APIneer.Api.Models;
using APIneer.Api.Proxy;
using APIneer.Api.Services;
using APIneer.Api.WebSocket;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Determine app data directory for key persistence
var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
var apineerKeysPath = Path.Combine(appDataPath, "APIneer", "keys");
Directory.CreateDirectory(apineerKeysPath);

// Configure Data Protection with persistent key storage
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(apineerKeysPath));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=apineer.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Credential protection service
builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();

// Proxy engine — uses IHttpClientFactory for connection pooling
builder.Services.AddTransient<IProxyEngine, ProxyEngine>();

// Auth handler — uses HttpClient for OAuth2 token requests
builder.Services.AddHttpClient<IAuthHandler, AuthHandler>();

// WebSocket proxy — singleton so REST endpoints can access the active connection
builder.Services.AddSingleton<WebSocketProxy>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — allow the Nuxt frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Response compression (gzip + brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "text/plain", "application/javascript"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// HttpClient pooling for ProxyEngine — avoids socket exhaustion
builder.Services.AddHttpClient("ProxyEngine", client =>
{
    client.DefaultRequestHeaders.ConnectionClose = false;
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 20,
    EnableMultipleHttp2Connections = true
});

var app = builder.Build();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseResponseCompression();
app.UseCors();
app.UseWebSockets();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithTags("System");

// ─── Validation constants ───────────────────────────────────────
var validHttpMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
const int maxBodySize = 10 * 1024 * 1024; // 10 MB

// ─── Workspace endpoints (minimal — for FK seeding) ─────────────

app.MapPost("/api/workspaces", async (AppDbContext db, CreateWorkspaceDto dto) =>
{
    var now = DateTime.UtcNow;
    var workspace = new Workspace
    {
        Id = Guid.NewGuid(),
        Name = dto.Name,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Workspaces.Add(workspace);
    await db.SaveChangesAsync();
    return Results.Created($"/api/workspaces/{workspace.Id}",
        new { id = workspace.Id, name = workspace.Name });
}).WithTags("Workspaces");

// ─── Collection CRUD endpoints ──────────────────────────────────

app.MapPost("/api/collections", async (AppDbContext db, CreateCollectionDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name))
        return Results.BadRequest(new { error = "Name is required." });

    var now = DateTime.UtcNow;
    var collection = new Collection
    {
        Id = Guid.NewGuid(),
        Name = dto.Name,
        Description = dto.Description,
        WorkspaceId = dto.WorkspaceId,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Collections.Add(collection);
    await db.SaveChangesAsync();
    return Results.Created($"/api/collections/{collection.Id}", MapCollectionResponse(collection));
}).WithTags("Collections");

app.MapGet("/api/collections", async (AppDbContext db, int? page, int? pageSize) =>
{
    var query = db.Collections.AsNoTracking();
    var totalCount = await query.CountAsync();
    var p = page ?? 1;
    var ps = Math.Clamp(pageSize ?? 50, 1, 100);

    var collections = await query
        .OrderByDescending(c => c.UpdatedAt)
        .Skip((p - 1) * ps)
        .Take(ps)
        .ToListAsync();
    return Results.Ok(new { items = collections.Select(MapCollectionResponse), page = p, pageSize = ps, totalCount });
}).WithTags("Collections");

app.MapGet("/api/collections/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var collection = await db.Collections
        .AsNoTracking()
        .Include(c => c.Folders)
        .Include(c => c.Requests)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (collection is null) return Results.NotFound();

    return Results.Ok(MapCollectionDetail(collection));
}).WithTags("Collections");

app.MapPut("/api/collections/{id:guid}", async (Guid id, AppDbContext db, UpdateCollectionDto dto) =>
{
    var collection = await db.Collections.FindAsync(id);
    if (collection is null) return Results.NotFound();

    if (dto.Name is not null) collection.Name = dto.Name;
    collection.Description = dto.Description;
    collection.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(MapCollectionResponse(collection));
}).WithTags("Collections");

app.MapDelete("/api/collections/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var collection = await db.Collections
        .Include(c => c.Folders)
        .Include(c => c.Requests).ThenInclude(r => r.History)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (collection is null) return Results.NotFound();

    foreach (var request in collection.Requests)
        db.RequestHistory.RemoveRange(request.History);
    db.ApiRequests.RemoveRange(collection.Requests);
    db.CollectionFolders.RemoveRange(collection.Folders);
    db.Collections.Remove(collection);

    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Collections");

// ─── Folder endpoints ───────────────────────────────────────────

app.MapPost("/api/collections/{collectionId:guid}/folders", async (Guid collectionId, AppDbContext db, CreateFolderDto dto) =>
{
    var collection = await db.Collections.FindAsync(collectionId);
    if (collection is null) return Results.NotFound();

    if (string.IsNullOrWhiteSpace(dto.Name))
        return Results.BadRequest(new { error = "Name is required." });

    var maxOrder = await db.CollectionFolders
        .Where(f => f.CollectionId == collectionId && f.ParentFolderId == dto.ParentFolderId)
        .MaxAsync(f => (int?)f.SortOrder) ?? -1;

    var folder = new CollectionFolder
    {
        Id = Guid.NewGuid(),
        CollectionId = collectionId,
        ParentFolderId = dto.ParentFolderId,
        Name = dto.Name,
        SortOrder = maxOrder + 1
    };

    db.CollectionFolders.Add(folder);
    await db.SaveChangesAsync();

    return Results.Created($"/api/collections/{collectionId}/folders/{folder.Id}", new
    {
        folder.Id,
        folder.CollectionId,
        folder.ParentFolderId,
        folder.Name,
        folder.SortOrder
    });
}).WithTags("Folders");

app.MapDelete("/api/collections/{collectionId:guid}/folders/{folderId:guid}",
    async (Guid collectionId, Guid folderId, AppDbContext db) =>
{
    var folder = await db.CollectionFolders.FindAsync(folderId);
    if (folder is null || folder.CollectionId != collectionId) return Results.NotFound();

    // Collect all descendant folder IDs
    var allFolderIds = new List<Guid> { folderId };
    await CollectDescendantFolderIds(db, folderId, allFolderIds);

    // Delete request history, then requests in those folders
    var requests = await db.ApiRequests
        .Where(r => r.FolderId != null && allFolderIds.Contains(r.FolderId.Value))
        .Include(r => r.History)
        .ToListAsync();
    foreach (var req in requests)
        db.RequestHistory.RemoveRange(req.History);
    db.ApiRequests.RemoveRange(requests);

    // Delete all folders
    var foldersToDelete = await db.CollectionFolders
        .Where(f => allFolderIds.Contains(f.Id))
        .ToListAsync();
    db.CollectionFolders.RemoveRange(foldersToDelete);

    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Folders");

// ─── Reorder endpoint ───────────────────────────────────────────

app.MapPatch("/api/collections/{id:guid}/reorder", async (Guid id, AppDbContext db, ReorderDto dto) =>
{
    var collection = await db.Collections.FindAsync(id);
    if (collection is null) return Results.NotFound();

    for (int i = 0; i < dto.ItemIds.Length; i++)
    {
        var request = await db.ApiRequests.FindAsync(dto.ItemIds[i]);
        if (request is not null)
            request.SortOrder = i;
    }

    await db.SaveChangesAsync();
    return Results.Ok();
}).WithTags("Collections");

// ─── Duplicate endpoint ─────────────────────────────────────────

app.MapPost("/api/collections/{id:guid}/duplicate", async (Guid id, AppDbContext db) =>
{
    var collection = await db.Collections
        .Include(c => c.Folders)
        .Include(c => c.Requests)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (collection is null) return Results.NotFound();

    var now = DateTime.UtcNow;
    var newCollection = new Collection
    {
        Id = Guid.NewGuid(),
        WorkspaceId = collection.WorkspaceId,
        Name = $"{collection.Name} (Copy)",
        Description = collection.Description,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Collections.Add(newCollection);

    // Map old folder IDs → new folder IDs (BFS: parents before children)
    var folderIdMap = new Dictionary<Guid, Guid>();
    var allFolders = collection.Folders.ToList();
    var queue = new Queue<CollectionFolder>(allFolders.Where(f => f.ParentFolderId == null));

    while (queue.Count > 0)
    {
        var src = queue.Dequeue();
        var newFolderId = Guid.NewGuid();
        folderIdMap[src.Id] = newFolderId;

        Guid? newParentId = src.ParentFolderId.HasValue && folderIdMap.ContainsKey(src.ParentFolderId.Value)
            ? folderIdMap[src.ParentFolderId.Value]
            : null;

        db.CollectionFolders.Add(new CollectionFolder
        {
            Id = newFolderId,
            CollectionId = newCollection.Id,
            ParentFolderId = newParentId,
            Name = src.Name,
            SortOrder = src.SortOrder
        });

        foreach (var child in allFolders.Where(f => f.ParentFolderId == src.Id))
            queue.Enqueue(child);
    }

    // Duplicate requests
    foreach (var src in collection.Requests)
    {
        Guid? newFolderId = src.FolderId.HasValue && folderIdMap.ContainsKey(src.FolderId.Value)
            ? folderIdMap[src.FolderId.Value]
            : null;

        db.ApiRequests.Add(new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = newCollection.Id,
            FolderId = newFolderId,
            Name = src.Name,
            Method = src.Method,
            Url = src.Url,
            Headers = src.Headers,
            Body = src.Body,
            BodyType = src.BodyType,
            SortOrder = src.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    await db.SaveChangesAsync();

    // Reload with nested data for the response
    var duplicated = await db.Collections
        .Include(c => c.Folders)
        .Include(c => c.Requests)
        .FirstAsync(c => c.Id == newCollection.Id);

    return Results.Created($"/api/collections/{duplicated.Id}", MapCollectionDetail(duplicated));
}).WithTags("Collections");

// ─── Request CRUD endpoints ─────────────────────────────────────

app.MapPost("/api/requests", async (AppDbContext db, CreateRequestDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name))
        return Results.BadRequest(new { error = "Name is required." });

    if (string.IsNullOrWhiteSpace(dto.Url))
        return Results.BadRequest(new { error = "URL is required." });

    if (string.IsNullOrWhiteSpace(dto.Method) || !validHttpMethods.Contains(dto.Method))
        return Results.BadRequest(new { error = "Invalid HTTP method." });

    if (dto.CollectionId is null || dto.CollectionId == Guid.Empty)
        return Results.BadRequest(new { error = "CollectionId is required." });

    if (dto.Body is not null && dto.Body.Length > maxBodySize)
        return Results.StatusCode(413);

    // Auto-assign SortOrder when not specified
    int sortOrder;
    if (dto.SortOrder.HasValue)
    {
        sortOrder = dto.SortOrder.Value;
    }
    else if (dto.FolderId.HasValue)
    {
        var maxOrder = await db.ApiRequests
            .Where(r => r.FolderId == dto.FolderId.Value)
            .MaxAsync(r => (int?)r.SortOrder) ?? -1;
        sortOrder = maxOrder + 1;
    }
    else
    {
        var maxOrder = await db.ApiRequests
            .Where(r => r.CollectionId == dto.CollectionId!.Value && r.FolderId == null)
            .MaxAsync(r => (int?)r.SortOrder) ?? -1;
        sortOrder = maxOrder + 1;
    }

    var now = DateTime.UtcNow;
    var request = new ApiRequest
    {
        Id = Guid.NewGuid(),
        Name = dto.Name,
        Method = dto.Method.ToUpperInvariant(),
        Url = dto.Url,
        CollectionId = dto.CollectionId.Value,
        Headers = dto.Headers,
        Body = dto.Body,
        BodyType = dto.BodyType,
        FolderId = dto.FolderId,
        SortOrder = sortOrder,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.ApiRequests.Add(request);
    await db.SaveChangesAsync();

    return Results.Created($"/api/requests/{request.Id}", MapToResponse(request));
}).WithTags("Requests");

app.MapGet("/api/requests", async (AppDbContext db, int? page, int? pageSize) =>
{
    var query = db.ApiRequests.AsNoTracking();
    var totalCount = await query.CountAsync();
    var p = page ?? 1;
    var ps = Math.Clamp(pageSize ?? 50, 1, 100);

    var requests = await query
        .OrderByDescending(r => r.UpdatedAt)
        .Skip((p - 1) * ps)
        .Take(ps)
        .ToListAsync();
    return Results.Ok(new { items = requests.Select(MapToResponse), page = p, pageSize = ps, totalCount });
}).WithTags("Requests");

app.MapGet("/api/requests/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var request = await db.ApiRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    return request is null ? Results.NotFound() : Results.Ok(MapToResponse(request));
}).WithTags("Requests");

app.MapPut("/api/requests/{id:guid}", async (Guid id, AppDbContext db, UpdateRequestDto dto) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    if (dto.Name is not null) request.Name = dto.Name;
    if (dto.Method is not null)
    {
        if (!validHttpMethods.Contains(dto.Method))
            return Results.BadRequest(new { error = "Invalid HTTP method." });
        request.Method = dto.Method.ToUpperInvariant();
    }
    if (dto.Url is not null) request.Url = dto.Url;
    request.Headers = dto.Headers;
    request.Body = dto.Body;
    request.BodyType = dto.BodyType;
    request.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(MapToResponse(request));
}).WithTags("Requests");

app.MapDelete("/api/requests/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    db.ApiRequests.Remove(request);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Requests");

// ─── Request execution & history ────────────────────────────────

app.MapPost("/api/requests/{id:guid}/send", async (Guid id, AppDbContext db) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    // Stub response — real proxy engine integration deferred
    var responseStatus = 200;
    var responseBody = """{"message":"OK"}""";
    var responseHeaders = """{"Content-Type":"application/json"}""";
    var responseTimeMs = 0L;
    var responseSizeBytes = (long)(responseBody?.Length ?? 0);

    var history = new RequestHistory
    {
        Id = Guid.NewGuid(),
        RequestId = request.Id,
        Method = request.Method,
        Url = request.Url,
        RequestHeaders = request.Headers,
        RequestBody = request.Body,
        ResponseStatus = responseStatus,
        ResponseHeaders = responseHeaders,
        ResponseBody = responseBody,
        ResponseTimeMs = responseTimeMs,
        ResponseSizeBytes = responseSizeBytes,
        ExecutedAt = DateTime.UtcNow
    };

    db.RequestHistory.Add(history);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        responseStatus,
        responseHeaders,
        responseBody,
        responseTimeMs,
        responseSizeBytes
    });
}).WithTags("Requests");

app.MapGet("/api/requests/{id:guid}/history", async (Guid id, AppDbContext db, int? page, int? pageSize) =>
{
    var request = await db.ApiRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    if (request is null) return Results.NotFound();

    var query = db.RequestHistory.AsNoTracking().Where(h => h.RequestId == id);
    var totalCount = await query.CountAsync();
    var p = page ?? 1;
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    var history = await query
        .OrderByDescending(h => h.ExecutedAt)
        .Skip((p - 1) * ps)
        .Take(ps)
        .Select(h => new
        {
            h.Id,
            h.RequestId,
            h.Method,
            h.Url,
            h.RequestHeaders,
            h.RequestBody,
            h.ResponseStatus,
            h.ResponseHeaders,
            h.ResponseBody,
            h.ResponseTimeMs,
            h.ResponseSizeBytes,
            h.ExecutedAt
        })
        .ToListAsync();

    return Results.Ok(new { items = history, page = p, pageSize = ps, totalCount });
}).WithTags("Requests");

// ─── Global History endpoints ───────────────────────────────────

app.MapGet("/api/history", async (
    AppDbContext db,
    Guid? requestId,
    string? method,
    int? status,
    string? from,
    string? to,
    int? page,
    int? pageSize) =>
{
    var query = db.RequestHistory.AsNoTracking().AsQueryable();

    if (requestId.HasValue)
        query = query.Where(h => h.RequestId == requestId.Value);
    if (!string.IsNullOrWhiteSpace(method))
        query = query.Where(h => h.Method == method.ToUpperInvariant());
    if (status.HasValue)
        query = query.Where(h => h.ResponseStatus == status.Value);
    if (from is not null && DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var fromDate))
        query = query.Where(h => h.ExecutedAt >= fromDate);
    if (to is not null && DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var toDate))
        query = query.Where(h => h.ExecutedAt <= toDate);

    var totalCount = await query.CountAsync();
    var p = page ?? 1;
    var ps = pageSize ?? 20;

    var items = await query
        .OrderByDescending(h => h.ExecutedAt)
        .Skip((p - 1) * ps)
        .Take(ps)
        .Select(h => new
        {
            h.Id,
            h.RequestId,
            h.Method,
            h.Url,
            h.RequestHeaders,
            h.RequestBody,
            h.ResponseStatus,
            h.ResponseHeaders,
            h.ResponseBody,
            h.ResponseTimeMs,
            h.ResponseSizeBytes,
            h.ExecutedAt
        })
        .ToListAsync();

    return Results.Ok(new { items, page = p, pageSize = ps, totalCount });
}).WithTags("History");

app.MapDelete("/api/history", async (AppDbContext db) =>
{
    db.RequestHistory.RemoveRange(db.RequestHistory);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("History");

// ─── Code Generation endpoint ───────────────────────────────────

var validCodeLanguages = new HashSet<string>
    { "javascript-fetch", "javascript-axios", "python-requests", "csharp-httpclient", "curl" };

app.MapGet("/api/requests/{id:guid}/code", async (Guid id, AppDbContext db, string? language) =>
{
    if (string.IsNullOrWhiteSpace(language) || !validCodeLanguages.Contains(language))
        return Results.BadRequest(new { error = "Invalid or missing language parameter." });

    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    var code = GenerateCode(request, language);
    return Results.Ok(new { language, code, requestId = id });
}).WithTags("CodeGeneration");

// ─── Assertion endpoints ────────────────────────────────────────

app.MapPost("/api/requests/{id:guid}/assertions", async (Guid id, AppDbContext db, CreateAssertionDto dto) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    var assertion = new Assertion
    {
        Id = Guid.NewGuid(),
        RequestId = id,
        Type = dto.Type,
        Expected = dto.Expected,
        CreatedAt = DateTime.UtcNow
    };

    db.Assertions.Add(assertion);
    await db.SaveChangesAsync();

    return Results.Created($"/api/requests/{id}/assertions/{assertion.Id}", new
    {
        assertion.Id,
        assertion.RequestId,
        assertion.Type,
        assertion.Expected,
        assertion.CreatedAt
    });
}).WithTags("Assertions");

app.MapGet("/api/requests/{id:guid}/assertions", async (Guid id, AppDbContext db) =>
{
    var request = await db.ApiRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    if (request is null) return Results.NotFound();

    var assertions = await db.Assertions
        .AsNoTracking()
        .Where(a => a.RequestId == id)
        .OrderBy(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            a.RequestId,
            a.Type,
            a.Expected,
            a.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(assertions);
}).WithTags("Assertions");

app.MapPost("/api/requests/{id:guid}/test", async (Guid id, AppDbContext db) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    // Execute the request (stub — same as /send)
    var responseStatus = 200;
    var responseBody = """{"message":"OK"}""";
    var responseHeaders = """{"Content-Type":"application/json"}""";

    var assertions = await db.Assertions
        .Where(a => a.RequestId == id)
        .ToListAsync();

    var results = assertions.Select(a => EvaluateAssertion(a, responseStatus, responseBody, responseHeaders)).ToArray();

    return Results.Ok(new
    {
        requestId = id,
        responseStatus,
        allPassed = results.All(r => r.Passed),
        results
    });
}).WithTags("Assertions");

// ─── Environment CRUD endpoints ─────────────────────────────────

app.MapPost("/api/environments", async (AppDbContext db, CreateEnvironmentDto dto) =>
{
    var now = DateTime.UtcNow;
    var environment = new APIneer.Api.Models.Environment
    {
        Id = Guid.NewGuid(),
        Name = dto.Name,
        WorkspaceId = dto.WorkspaceId,
        IsActive = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Environments.Add(environment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/environments/{environment.Id}", MapToEnvironmentResponse(environment));
}).WithTags("Environments");

app.MapGet("/api/environments", async (AppDbContext db) =>
{
    var environments = await db.Environments
        .AsNoTracking()
        .Include(e => e.Variables)
        .ToListAsync();

    return Results.Ok(environments.Select(e => MapToEnvironmentResponse(e, maskSecrets: true)));
}).WithTags("Environments");

app.MapGet("/api/environments/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var environment = await db.Environments
        .AsNoTracking()
        .Include(e => e.Variables)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (environment is null) return Results.NotFound();

    return Results.Ok(MapToEnvironmentResponse(environment, maskSecrets: true));
}).WithTags("Environments");

app.MapPut("/api/environments/{id:guid}", async (Guid id, AppDbContext db, UpdateEnvironmentDto dto) =>
{
    var environment = await db.Environments
        .Include(e => e.Variables)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (environment is null) return Results.NotFound();

    if (dto.Name is not null) environment.Name = dto.Name;
    environment.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(MapToEnvironmentResponse(environment, maskSecrets: true));
}).WithTags("Environments");

app.MapDelete("/api/environments/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var environment = await db.Environments
        .Include(e => e.Variables)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (environment is null) return Results.NotFound();

    db.Environments.Remove(environment);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Environments");

// ─── Environment Variable endpoints ─────────────────────────────

app.MapPost("/api/environments/{id:guid}/variables", async (Guid id, AppDbContext db, ICredentialProtector protector, ILogger<Program> logger, CreateVariableDto dto) =>
{
    var environment = await db.Environments.FindAsync(id);
    if (environment is null) return Results.NotFound();

    var now = DateTime.UtcNow;
    string encryptedValue = dto.Value;
    
    // Encrypt secret values before storage
    if (dto.IsSecret)
    {
        try
        {
            var encryptedBytes = protector.Encrypt(dto.Value);
            encryptedValue = Convert.ToBase64String(encryptedBytes);
            logger.LogInformation("Secret variable created for key '{Key}' with encryption", dto.Key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to encrypt secret variable '{Key}'", dto.Key);
            return Results.BadRequest(new { error = "Failed to encrypt secret value" });
        }
    }
    
    var variable = new EnvironmentVariable
    {
        Id = Guid.NewGuid(),
        EnvironmentId = id,
        Key = dto.Key,
        Value = encryptedValue,
        IsSecret = dto.IsSecret,
        CreatedAt = now
    };
    db.EnvironmentVariables.Add(variable);
    await db.SaveChangesAsync();

    return Results.Created($"/api/environments/{id}/variables/{variable.Id}", MapToVariableResponse(variable, maskSecrets: true));
}).WithTags("Environments");

app.MapGet("/api/environments/{id:guid}/variables/{varId:guid}", async (Guid id, AppDbContext db, Guid varId) =>
{
    var environment = await db.Environments.FindAsync(id);
    if (environment is null) return Results.NotFound();

    var variable = await db.EnvironmentVariables
        .FirstOrDefaultAsync(v => v.Id == varId && v.EnvironmentId == id);
    if (variable is null) return Results.NotFound();

    return Results.Ok(MapToVariableResponse(variable, maskSecrets: true));
}).WithTags("Environments");

app.MapPut("/api/environments/{id:guid}/variables/{varId:guid}", async (Guid id, AppDbContext db, ICredentialProtector protector, ILogger<Program> logger, Guid varId, UpdateVariableDto dto) =>
{
    var variable = await db.EnvironmentVariables
        .FirstOrDefaultAsync(v => v.Id == varId && v.EnvironmentId == id);
    if (variable is null) return Results.NotFound();

    if (dto.Key is not null) variable.Key = dto.Key;
    if (dto.Value is not null)
    {
        // Encrypt new secret values before storage
        if (dto.IsSecret)
        {
            try
            {
                var encryptedBytes = protector.Encrypt(dto.Value);
                variable.Value = Convert.ToBase64String(encryptedBytes);
                logger.LogInformation("Secret variable updated for key '{Key}' with encryption", variable.Key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to encrypt secret variable '{Key}'", variable.Key);
                return Results.BadRequest(new { error = "Failed to encrypt secret value" });
            }
        }
        else
        {
            variable.Value = dto.Value;
        }
    }
    variable.IsSecret = dto.IsSecret;

    await db.SaveChangesAsync();
    return Results.Ok(MapToVariableResponse(variable, maskSecrets: true));
}).WithTags("Environments");

app.MapDelete("/api/environments/{id:guid}/variables/{varId:guid}", async (Guid id, AppDbContext db, Guid varId) =>
{
    var variable = await db.EnvironmentVariables
        .FirstOrDefaultAsync(v => v.Id == varId && v.EnvironmentId == id);
    if (variable is null) return Results.NotFound();

    db.EnvironmentVariables.Remove(variable);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Environments");

// ─── Activate / Deactivate endpoints ────────────────────────────

app.MapPut("/api/environments/{id:guid}/activate", async (Guid id, AppDbContext db) =>
{
    var environment = await db.Environments.FindAsync(id);
    if (environment is null) return Results.NotFound();

    // Deactivate all other environments in the same workspace
    var siblings = await db.Environments
        .Where(e => e.WorkspaceId == environment.WorkspaceId && e.IsActive)
        .ToListAsync();
    foreach (var sibling in siblings)
    {
        sibling.IsActive = false;
    }

    environment.IsActive = true;
    await db.SaveChangesAsync();

    return Results.Ok(MapToEnvironmentResponse(environment));
}).WithTags("Environments");

app.MapPut("/api/environments/{id:guid}/deactivate", async (Guid id, AppDbContext db) =>
{
    var environment = await db.Environments.FindAsync(id);
    if (environment is null) return Results.NotFound();

    environment.IsActive = false;
    await db.SaveChangesAsync();

    return Results.Ok(MapToEnvironmentResponse(environment));
}).WithTags("Environments");

// ─── Variable Resolution endpoint ──────────────────────────────

app.MapPost("/api/environments/resolve", async (AppDbContext db, ICredentialProtector protector, ILogger<Program> logger, ResolveRequestDto dto) =>
{
    // Find the active environment (across all workspaces — there should be at most one active)
    var activeEnv = await db.Environments
        .Include(e => e.Variables)
        .FirstOrDefaultAsync(e => e.IsActive);

    var variables = new Dictionary<string, string>();
    
    if (activeEnv?.Variables != null)
    {
        foreach (var v in activeEnv.Variables)
        {
            if (v.IsSecret)
            {
                try
                {
                    var encryptedBytes = Convert.FromBase64String(v.Value);
                    var decrypted = protector.Decrypt(encryptedBytes);
                    variables[v.Key] = decrypted;
                    logger.LogInformation("Secret variable '{Key}' decrypted for resolution", v.Key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to decrypt secret variable '{Key}'", v.Key);
                    return Results.BadRequest(new { error = $"Failed to decrypt secret variable '{v.Key}'" });
                }
            }
            else
            {
                variables[v.Key] = v.Value;
            }
        }
    }

    var resolvedUrl = ResolveVariables(dto.Url ?? "", variables);
    var resolvedHeaders = ResolveVariables(dto.Headers, variables);
    var resolvedBody = ResolveVariables(dto.Body, variables);

    return Results.Ok(new { url = resolvedUrl, headers = resolvedHeaders, body = resolvedBody });
}).WithTags("Environments");

// ─── Move request endpoint ──────────────────────────────────────

app.MapPatch("/api/requests/{id:guid}/move", async (Guid id, AppDbContext db, MoveRequestDto dto) =>
{
    var request = await db.ApiRequests.FindAsync(id);
    if (request is null) return Results.NotFound();

    request.FolderId = dto.FolderId;
    request.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(MapToResponse(request));
}).WithTags("Requests");

// ─── Import endpoints ──────────────────────────────────────────

app.MapPost("/api/import/postman", async (AppDbContext db, HttpRequest httpReq) =>
{
    string body;
    using (var reader = new StreamReader(httpReq.Body))
        body = await reader.ReadToEndAsync();

    // Parse the wrapper: { "collection": "<postman json string>" }
    string postmanJson;
    try
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("collection", out var collectionProp))
            return Results.BadRequest(new { error = "Missing 'collection' field." });

        postmanJson = collectionProp.GetString() ?? "";
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body." });
    }

    if (string.IsNullOrWhiteSpace(postmanJson))
        return Results.BadRequest(new { error = "Collection body is empty." });

    try
    {
        var result = await PostmanImporter.ImportAsync(db, postmanJson);
        return Results.Ok(new
        {
            result.CollectionId,
            result.CollectionName,
            result.RequestCount,
            result.FolderCount
        });
    }
    catch (ImportValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithTags("Import");

app.MapPost("/api/import/curl", async (AppDbContext db, HttpRequest httpReq) =>
{
    string curlCommand;
    using (var reader = new StreamReader(httpReq.Body))
        curlCommand = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(curlCommand))
        return Results.BadRequest(new { error = "cURL command is empty." });

    try
    {
        var parsed = CurlImporter.Parse(curlCommand);

        // Create a workspace and collection for the imported request
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "cURL Import",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Workspaces.Add(workspace);

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = "cURL Imports",
            WorkspaceId = workspace.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Collections.Add(collection);

        var apiRequest = new ApiRequest
        {
            Id = Guid.NewGuid(),
            CollectionId = collection.Id,
            Name = parsed.Name,
            Method = parsed.Method,
            Url = parsed.Url,
            Headers = parsed.Headers,
            Body = parsed.Body,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ApiRequests.Add(apiRequest);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            RequestId = apiRequest.Id,
            apiRequest.Name,
            apiRequest.Method,
            apiRequest.Url,
            apiRequest.Headers,
            apiRequest.Body
        });
    }
    catch (ImportValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithTags("Import");

app.MapPost("/api/import/json", async (AppDbContext db, HttpRequest httpReq) =>
{
    string body;
    using (var reader = new StreamReader(httpReq.Body))
        body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(new { error = "Request body is empty." });

    try
    {
        var result = await JsonImporter.ImportAsync(db, body);
        return Results.Ok(new
        {
            result.CollectionId,
            result.CollectionName,
            result.RequestCount,
            result.FolderCount
        });
    }
    catch (ImportValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithTags("Import");

// ─── Export endpoint ───────────────────────────────────────────

app.MapGet("/api/collections/{id:guid}/export", async (Guid id, string? format, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(format) ||
        !new[] { "json", "curl", "postman" }.Contains(format.ToLowerInvariant()))
    {
        return Results.BadRequest(new { error = "Invalid or missing format. Supported: json, curl, postman." });
    }

    var collection = await db.Collections
        .AsNoTracking()
        .Include(c => c.Folders)
        .Include(c => c.Requests)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (collection is null)
        return Results.NotFound();

    return format.ToLowerInvariant() switch
    {
        "json" => Results.Ok(JsonExporter.Export(collection)),
        "curl" => Results.Ok(CurlExporter.Export(collection)),
        "postman" => Results.Ok(PostmanExporter.Export(collection)),
        _ => Results.BadRequest(new { error = "Unsupported format." })
    };
}).WithTags("Export");

// ─── Helpers ────────────────────────────────────────────────────

static object MapToResponse(ApiRequest r) => new
{
    r.Id,
    r.CollectionId,
    r.FolderId,
    r.Name,
    r.Method,
    r.Url,
    r.Headers,
    r.Body,
    r.BodyType,
    r.SortOrder,
    r.CreatedAt,
    r.UpdatedAt
};

static object MapCollectionResponse(Collection c) => new
{
    c.Id,
    c.WorkspaceId,
    c.Name,
    c.Description,
    c.CreatedAt,
    c.UpdatedAt
};

static object MapCollectionDetail(Collection c)
{
    var allFolders = c.Folders.ToList();
    var allRequests = c.Requests.ToList();

    var rootFolders = allFolders
        .Where(f => f.ParentFolderId == null)
        .OrderBy(f => f.SortOrder)
        .Select(f => MapFolderTree(f, allFolders, allRequests))
        .ToArray();

    var rootRequests = allRequests
        .Where(r => r.FolderId == null)
        .OrderBy(r => r.SortOrder)
        .Select(MapRequestSummary)
        .ToArray();

    return new
    {
        c.Id,
        c.WorkspaceId,
        c.Name,
        c.Description,
        c.CreatedAt,
        c.UpdatedAt,
        folders = rootFolders,
        requests = rootRequests
    };
}

static object MapFolderTree(CollectionFolder f, List<CollectionFolder> allFolders, List<ApiRequest> allRequests)
{
    var subFolders = allFolders
        .Where(sf => sf.ParentFolderId == f.Id)
        .OrderBy(sf => sf.SortOrder)
        .Select(sf => MapFolderTree(sf, allFolders, allRequests))
        .ToArray();

    var folderRequests = allRequests
        .Where(r => r.FolderId == f.Id)
        .OrderBy(r => r.SortOrder)
        .Select(MapRequestSummary)
        .ToArray();

    return new
    {
        f.Id,
        f.CollectionId,
        f.ParentFolderId,
        f.Name,
        f.SortOrder,
        subFolders,
        requests = folderRequests
    };
}

static object MapRequestSummary(ApiRequest r) => new
{
    r.Id,
    r.CollectionId,
    r.FolderId,
    r.Name,
    r.Method,
    r.Url,
    r.Headers,
    r.Body,
    r.BodyType,
    r.SortOrder
};

static async Task CollectDescendantFolderIds(AppDbContext db, Guid parentId, List<Guid> result)
{
    var childIds = await db.CollectionFolders
        .Where(f => f.ParentFolderId == parentId)
        .Select(f => f.Id)
        .ToListAsync();

    foreach (var childId in childIds)
    {
        result.Add(childId);
        await CollectDescendantFolderIds(db, childId, result);
    }
}

static object MapToEnvironmentResponse(APIneer.Api.Models.Environment e, bool maskSecrets = false) => new
{
    id = e.Id,
    workspaceId = e.WorkspaceId,
    name = e.Name,
    isActive = e.IsActive,
    createdAt = e.CreatedAt,
    updatedAt = e.UpdatedAt,
    variables = e.Variables?.Select(v => MapToVariableResponse(v, maskSecrets)).ToArray()
        ?? Array.Empty<object>()
};

static object MapToVariableResponse(EnvironmentVariable v, bool maskSecrets = false) => new
{
    id = v.Id,
    environmentId = v.EnvironmentId,
    key = v.Key,
    value = maskSecrets && v.IsSecret ? "***masked***" : v.Value,
    isSecret = v.IsSecret,
    createdAt = v.CreatedAt
};

/// <summary>
/// Resolves {{variable}} placeholders in a string using the provided variables dictionary.
/// Escaped braces \{\{ and \}\} are converted to literal {{ and }} without resolution.
/// Undefined variables are left as-is.
/// </summary>
static string? ResolveVariables(string? input, Dictionary<string, string> variables)
{
    if (input is null) return null;

    // Replace escaped braces with temporary placeholders
    const string escapedOpen = @"\{\{";
    const string escapedClose = @"\}\}";
    const string placeholderOpen = "\x01OPEN\x01";
    const string placeholderClose = "\x01CLOSE\x01";

    var result = input.Replace(escapedOpen, placeholderOpen)
                      .Replace(escapedClose, placeholderClose);

    // Resolve {{key}} patterns
    result = System.Text.RegularExpressions.Regex.Replace(
        result,
        @"\{\{(\w+)\}\}",
        match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });

    // Restore escaped braces as literal {{ and }}
    result = result.Replace(placeholderOpen, "{{")
                   .Replace(placeholderClose, "}}");

    return result;
}

// ─── WebSocket proxy endpoints ──────────────────────────────────

app.MapGet("/api/ws/connect", async (HttpContext context, WebSocketProxy wsProxy, string? url) =>
{
    if (string.IsNullOrWhiteSpace(url))
        return Results.BadRequest(new { error = "Query parameter 'url' is required." });

    if (context.WebSockets.IsWebSocketRequest)
    {
        await WebSocketProxy.HandleUpgradeAsync(context, url, wsProxy);
        return Results.Empty;
    }

    // Non-WebSocket client: connect via REST and return status
    try
    {
        await wsProxy.ConnectAsync(url, context.RequestAborted);
        return Results.Ok(new
        {
            status = wsProxy.Status.ToString().ToLowerInvariant(),
            targetUrl = wsProxy.TargetUrl
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            status = wsProxy.Status.ToString().ToLowerInvariant(),
            error = ex.Message
        });
    }
}).WithTags("WebSocket");

app.MapPost("/api/ws/send", async (WebSocketProxy wsProxy, SendMessageDto dto) =>
{
    if (wsProxy.Status != WebSocketConnectionStatus.Open)
        return Results.BadRequest(new { error = "No active WebSocket connection." });

    if (string.IsNullOrEmpty(dto.Message))
        return Results.BadRequest(new { error = "Message is required." });

    try
    {
        await wsProxy.SendAsync(dto.Message);
        return Results.Ok(new { status = "sent", message = dto.Message });
    }
    catch
    {
        return Results.StatusCode(502);
    }
}).WithTags("WebSocket");

app.MapGet("/api/ws/messages", (WebSocketProxy wsProxy) =>
{
    return Results.Ok(new
    {
        status = wsProxy.Status.ToString().ToLowerInvariant(),
        messages = wsProxy.Messages.Select(m => new
        {
            direction = m.Direction,
            content = m.Content,
            timestamp = m.Timestamp
        })
    });
}).WithTags("WebSocket");

app.MapDelete("/api/ws/disconnect", async (WebSocketProxy wsProxy) =>
{
    await wsProxy.DisconnectAsync();
    return Results.Ok(new
    {
        status = wsProxy.Status.ToString().ToLowerInvariant(),
        message = wsProxy.Status == WebSocketConnectionStatus.Closed
            ? "Disconnected successfully."
            : "No active connection."
    });
}).WithTags("WebSocket");

app.MapGet("/api/ws/status", (WebSocketProxy wsProxy) =>
{
    return Results.Ok(new
    {
        status = wsProxy.Status.ToString().ToLowerInvariant(),
        targetUrl = wsProxy.TargetUrl,
        messageCount = wsProxy.Messages.Count,
        error = wsProxy.ErrorMessage
    });
}).WithTags("WebSocket");

app.Run();

// ─── Code generation helpers ────────────────────────────────────

static string GenerateCode(ApiRequest request, string language)
{
    Dictionary<string, string>? headers = null;
    if (!string.IsNullOrEmpty(request.Headers))
    {
        try { headers = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Headers); }
        catch { /* ignore parse errors */ }
    }

    return language switch
    {
        "javascript-fetch" => GenerateFetchCode(request, headers),
        "javascript-axios" => GenerateAxiosCode(request, headers),
        "python-requests" => GeneratePythonCode(request, headers),
        "csharp-httpclient" => GenerateCSharpCode(request, headers),
        "curl" => GenerateCurlCode(request, headers),
        _ => string.Empty
    };
}

static string GenerateFetchCode(ApiRequest request, Dictionary<string, string>? headers)
{
    var sb = new StringBuilder();
    sb.AppendLine($"fetch('{request.Url}', {{");
    sb.AppendLine($"  method: '{request.Method}',");

    if (headers is { Count: > 0 })
    {
        sb.AppendLine("  headers: {");
        foreach (var (key, value) in headers)
            sb.AppendLine($"    '{key}': '{value}',");
        sb.AppendLine("  },");
    }

    if (!string.IsNullOrEmpty(request.Body))
    {
        sb.AppendLine($"  body: JSON.stringify({request.Body}),");
    }

    sb.AppendLine("})");
    sb.AppendLine(".then(response => response.json())");
    sb.AppendLine(".then(data => console.log(data));");
    return sb.ToString();
}

static string GenerateAxiosCode(ApiRequest request, Dictionary<string, string>? headers)
{
    var sb = new StringBuilder();
    var methodLower = request.Method.ToLowerInvariant();

    sb.AppendLine("const axios = require('axios');");
    sb.AppendLine();
    sb.AppendLine($"axios.{methodLower}('{request.Url}', {{");

    if (headers is { Count: > 0 })
    {
        sb.AppendLine("  headers: {");
        foreach (var (key, value) in headers)
            sb.AppendLine($"    '{key}': '{value}',");
        sb.AppendLine("  },");
    }

    if (!string.IsNullOrEmpty(request.Body))
    {
        sb.AppendLine($"  data: {request.Body},");
    }

    sb.AppendLine("})");
    sb.AppendLine(".then(response => console.log(response.data));");
    return sb.ToString();
}

static string GeneratePythonCode(ApiRequest request, Dictionary<string, string>? headers)
{
    var sb = new StringBuilder();
    var methodLower = request.Method.ToLowerInvariant();

    sb.AppendLine("import requests");
    sb.AppendLine();
    sb.Append($"response = requests.{methodLower}('{request.Url}'");

    if (headers is { Count: > 0 })
    {
        sb.AppendLine(",");
        sb.AppendLine("    headers={");
        foreach (var (key, value) in headers)
            sb.AppendLine($"        '{key}': '{value}',");
        sb.Append("    }");
    }

    if (!string.IsNullOrEmpty(request.Body))
    {
        sb.AppendLine(",");
        sb.Append($"    json={request.Body}");
    }

    sb.AppendLine(")");
    sb.AppendLine("print(response.json())");
    return sb.ToString();
}

static string GenerateCSharpCode(ApiRequest request, Dictionary<string, string>? headers)
{
    var sb = new StringBuilder();
    sb.AppendLine("using var client = new HttpClient();");
    sb.AppendLine($"var request = new HttpRequestMessage(HttpMethod.{CapitalizeMethod(request.Method)}, \"{request.Url}\");");

    if (headers is { Count: > 0 })
    {
        foreach (var (key, value) in headers)
        {
            sb.AppendLine($"request.Headers.TryAddWithoutValidation(\"{key}\", \"{value}\");");
        }
    }

    if (!string.IsNullOrEmpty(request.Body))
    {
        var contentType = headers?.GetValueOrDefault("Content-Type") ?? "text/plain";
        sb.AppendLine($"request.Content = new StringContent(\"{EscapeCSharpString(request.Body)}\", Encoding.UTF8, \"{contentType}\");");
    }

    sb.AppendLine("var response = await client.SendAsync(request);");
    sb.AppendLine("var body = await response.Content.ReadAsStringAsync();");
    return sb.ToString();
}

static string GenerateCurlCode(ApiRequest request, Dictionary<string, string>? headers)
{
    var sb = new StringBuilder();
    sb.Append($"curl -X {request.Method} '{request.Url}'");

    if (headers is { Count: > 0 })
    {
        foreach (var (key, value) in headers)
            sb.Append($" \\\n  -H '{key}: {value}'");
    }

    if (!string.IsNullOrEmpty(request.Body))
    {
        sb.Append($" \\\n  -d '{request.Body}'");
    }

    sb.AppendLine();
    return sb.ToString();
}

static string CapitalizeMethod(string method)
{
    return method.ToUpperInvariant() switch
    {
        "GET" => "Get",
        "POST" => "Post",
        "PUT" => "Put",
        "DELETE" => "Delete",
        "PATCH" => "Patch",
        "HEAD" => "Head",
        "OPTIONS" => "Options",
        _ => method
    };
}

static string EscapeCSharpString(string value)
{
    return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

static AssertionResult EvaluateAssertion(Assertion assertion, int responseStatus, string? responseBody, string? responseHeaders)
{
    return assertion.Type switch
    {
        "status_equals" => new AssertionResult(
            assertion.Type,
            assertion.Expected,
            responseStatus.ToString() == assertion.Expected,
            responseStatus.ToString()),
        "body_contains" => new AssertionResult(
            assertion.Type,
            assertion.Expected,
            responseBody?.Contains(assertion.Expected, StringComparison.Ordinal) ?? false,
            responseBody?.Contains(assertion.Expected, StringComparison.Ordinal) == true ? "Found" : "Not found"),
        "header_exists" => new AssertionResult(
            assertion.Type,
            assertion.Expected,
            responseHeaders?.Contains(assertion.Expected, StringComparison.OrdinalIgnoreCase) ?? false,
            responseHeaders?.Contains(assertion.Expected, StringComparison.OrdinalIgnoreCase) == true ? "Present" : "Missing"),
        _ => new AssertionResult(assertion.Type, assertion.Expected, false, "Unknown assertion type")
    };
}

// ─── DTOs ───────────────────────────────────────────────────────

record CreateWorkspaceDto(string Name);
record CreateCollectionDto(string? Name, string? Description, Guid WorkspaceId);
record UpdateCollectionDto(string? Name, string? Description);
record CreateFolderDto(string? Name, Guid? ParentFolderId);
record MoveRequestDto(Guid? FolderId);
record ReorderDto(Guid[] ItemIds);

record CreateRequestDto(
    string? Name,
    string? Method,
    string? Url,
    Guid? CollectionId,
    string? Headers,
    string? Body,
    string? BodyType,
    Guid? FolderId,
    int? SortOrder);

record UpdateRequestDto(
    string? Name,
    string? Method,
    string? Url,
    string? Headers,
    string? Body,
    string? BodyType,
    int? SortOrder);

record CreateEnvironmentDto(string Name, Guid WorkspaceId);
record UpdateEnvironmentDto(string? Name);

record CreateVariableDto(string Key, string Value, bool IsSecret = false);
record UpdateVariableDto(string? Key, string? Value, bool IsSecret = false);

record ResolveRequestDto(string? Url, string? Headers, string? Body);

record CreateAssertionDto(string Type, string Expected);

record AssertionResult(string Type, string Expected, bool Passed, string? Actual);

/// <summary>
/// Enables WebApplicationFactory access from the test project.
/// </summary>
public partial class Program;
