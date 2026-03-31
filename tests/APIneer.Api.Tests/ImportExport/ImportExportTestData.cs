using System.Text.Json;
using System.Text.Json.Serialization;
using APIneer.Api.Tests.Requests;

namespace APIneer.Api.Tests.ImportExport;

/// <summary>
/// Shared test data, helpers, and DTOs for Import/Export tests.
/// Defines the expected API contract for import/export functionality.
/// </summary>
public static class ImportExportTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ──────────────────────────────────────────────
    // Seed helpers
    // ──────────────────────────────────────────────

    public static async Task<Guid> SeedWorkspaceAsync(HttpClient client)
    {
        var payload = new { name = "Import/Export Test Workspace" };
        var response = await client.PostAsync("/api/workspaces",
            TestData.JsonContent(payload));

        if (response.IsSuccessStatusCode)
        {
            var body = await Deserialize<IdResponse>(response);
            return body!.Id;
        }

        return TestData.WellKnownWorkspaceId;
    }

    public static async Task<Guid> SeedCollectionWithRequestsAsync(HttpClient client)
    {
        var workspaceId = await SeedWorkspaceAsync(client);

        // Create collection
        var colPayload = new { name = "Export Test Collection", description = "For export tests", workspaceId };
        var colResponse = await client.PostAsync("/api/collections",
            TestData.JsonContent(colPayload));
        var col = await Deserialize<IdResponse>(colResponse);
        var collectionId = col!.Id;

        // Create a folder
        var folderPayload = new { name = "Users Folder" };
        var folderResponse = await client.PostAsync($"/api/collections/{collectionId}/folders",
            TestData.JsonContent(folderPayload));
        var folder = await Deserialize<IdResponse>(folderResponse);
        var folderId = folder!.Id;

        // Create requests
        var req1 = new
        {
            name = "List Users",
            method = "GET",
            url = "https://api.example.com/users",
            headers = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Accept"] = "application/json",
                ["Authorization"] = "Bearer token123"
            }),
            collectionId,
            folderId
        };
        await client.PostAsync("/api/requests", TestData.JsonContent(req1));

        var req2 = new
        {
            name = "Create User",
            method = "POST",
            url = "https://api.example.com/users",
            headers = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }),
            body = """{"username":"newuser","email":"new@example.com"}""",
            bodyType = "application/json",
            collectionId,
            folderId
        };
        await client.PostAsync("/api/requests", TestData.JsonContent(req2));

        // Root-level request (no folder)
        var req3 = new
        {
            name = "Health Check",
            method = "GET",
            url = "https://api.example.com/health",
            collectionId
        };
        await client.PostAsync("/api/requests", TestData.JsonContent(req3));

        return collectionId;
    }

    // ──────────────────────────────────────────────
    // Postman v2.1 fixture
    // ──────────────────────────────────────────────

    public const string PostmanV21Collection = """
    {
      "info": {
        "_postman_id": "abc12345-def6-7890-abcd-ef1234567890",
        "name": "Sample API Collection",
        "description": "A sample Postman collection for testing import",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
      },
      "item": [
        {
          "name": "Users",
          "item": [
            {
              "name": "Get All Users",
              "request": {
                "method": "GET",
                "header": [
                  {
                    "key": "Accept",
                    "value": "application/json"
                  },
                  {
                    "key": "Authorization",
                    "value": "Bearer {{token}}"
                  }
                ],
                "url": {
                  "raw": "https://api.example.com/users?page=1&limit=10",
                  "protocol": "https",
                  "host": ["api", "example", "com"],
                  "path": ["users"],
                  "query": [
                    { "key": "page", "value": "1" },
                    { "key": "limit", "value": "10" }
                  ]
                }
              }
            },
            {
              "name": "Create User",
              "request": {
                "method": "POST",
                "header": [
                  {
                    "key": "Content-Type",
                    "value": "application/json"
                  }
                ],
                "body": {
                  "mode": "raw",
                  "raw": "{\"username\":\"newuser\",\"email\":\"new@example.com\"}",
                  "options": {
                    "raw": {
                      "language": "json"
                    }
                  }
                },
                "url": {
                  "raw": "https://api.example.com/users",
                  "protocol": "https",
                  "host": ["api", "example", "com"],
                  "path": ["users"]
                }
              }
            },
            {
              "name": "Auth",
              "item": [
                {
                  "name": "Login",
                  "request": {
                    "method": "POST",
                    "header": [
                      {
                        "key": "Content-Type",
                        "value": "application/json"
                      }
                    ],
                    "body": {
                      "mode": "raw",
                      "raw": "{\"username\":\"admin\",\"password\":\"secret\"}"
                    },
                    "url": {
                      "raw": "https://api.example.com/auth/login",
                      "protocol": "https",
                      "host": ["api", "example", "com"],
                      "path": ["auth", "login"]
                    }
                  }
                }
              ]
            }
          ]
        },
        {
          "name": "Health Check",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "https://api.example.com/health",
              "protocol": "https",
              "host": ["api", "example", "com"],
              "path": ["health"]
            }
          }
        }
      ]
    }
    """;

    public const string PostmanV21CollectionNested = """
    {
      "info": {
        "_postman_id": "nested-test-collection",
        "name": "Deeply Nested Collection",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
      },
      "item": [
        {
          "name": "Level 1",
          "item": [
            {
              "name": "Level 2",
              "item": [
                {
                  "name": "Level 3",
                  "item": [
                    {
                      "name": "Deep Request",
                      "request": {
                        "method": "DELETE",
                        "header": [],
                        "url": {
                          "raw": "https://api.example.com/deep/resource/123",
                          "protocol": "https",
                          "host": ["api", "example", "com"],
                          "path": ["deep", "resource", "123"]
                        }
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    // ──────────────────────────────────────────────
    // cURL test fixtures
    // ──────────────────────────────────────────────

    public const string SimpleCurlGet = "curl https://api.example.com/users";

    public const string CurlWithMethod = "curl -X POST https://api.example.com/users";

    public const string CurlWithHeaders = """curl -X GET https://api.example.com/users -H "Accept: application/json" -H "Authorization: Bearer token123" """;

    public const string CurlWithBody = """curl -X POST https://api.example.com/users -H "Content-Type: application/json" -d '{"username":"test","email":"test@example.com"}' """;

    public const string CurlWithDataFlag = """curl -X PUT https://api.example.com/users/1 --data '{"name":"updated"}' """;

    public const string CurlMultiline = """
    curl -X POST \
      https://api.example.com/users \
      -H 'Content-Type: application/json' \
      -H 'Authorization: Bearer mytoken' \
      -d '{"username":"multiline","email":"multi@example.com"}'
    """;

    public const string CurlWithBasicAuth = """curl -u admin:password123 https://api.example.com/admin/dashboard""";

    // ──────────────────────────────────────────────
    // Response DTOs
    // ──────────────────────────────────────────────

    public record IdResponse(Guid Id);

    public record ImportResultResponse(
        Guid CollectionId,
        string CollectionName,
        int RequestCount,
        int FolderCount);

    public record CurlImportResultResponse(
        Guid RequestId,
        string Name,
        string Method,
        string Url,
        string? Headers,
        string? Body);

    public record ExportJsonResponse(
        Guid Id,
        string Name,
        string? Description,
        ExportFolderResponse[]? Folders,
        ExportRequestResponse[]? Requests);

    public record ExportFolderResponse(
        string Name,
        ExportFolderResponse[]? SubFolders,
        ExportRequestResponse[]? Requests);

    public record ExportRequestResponse(
        string Name,
        string Method,
        string Url,
        string? Headers,
        string? Body,
        string? BodyType);

    public record ExportCurlResponse(
        string RequestName,
        string CurlCommand);

    public record ExportCurlCollectionResponse(
        string CollectionName,
        ExportCurlResponse[] Requests);

    public record PostmanExportResponse(
        PostmanExportInfo Info,
        PostmanExportItem[] Item);

    public record PostmanExportInfo(
        string Name,
        string? Description,
        string Schema);

    public record PostmanExportItem(
        string Name,
        PostmanExportRequest? Request,
        PostmanExportItem[]? Item);

    public record PostmanExportRequest(
        string Method,
        PostmanExportHeader[]? Header,
        PostmanExportBody? Body,
        PostmanExportUrl Url);

    public record PostmanExportHeader(string Key, string Value);

    public record PostmanExportBody(string Mode, string Raw);

    public record PostmanExportUrl(string Raw);

    // ──────────────────────────────────────────────
    // Serialization helpers
    // ──────────────────────────────────────────────

    public static StringContent JsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    public static StringContent TextContent(string text)
    {
        return new StringContent(text, System.Text.Encoding.UTF8, "text/plain");
    }

    public static async Task<T?> Deserialize<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    public static async Task<string> ReadBody(HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync();
    }
}
