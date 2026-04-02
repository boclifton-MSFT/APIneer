using System.Text;
using System.Text.Json;
using APIneer.Api.Models;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Exports collection requests as cURL commands.
/// </summary>
public static class CurlExporter
{
    public static object Export(Collection collection)
    {
        var requests = collection.Requests
            .OrderBy(r => r.SortOrder)
            .Select(r => new
            {
                RequestName = r.Name,
                CurlCommand = GenerateCurl(r)
            })
            .ToArray();

        return new
        {
            CollectionName = collection.Name,
            Requests = requests
        };
    }

    private static string GenerateCurl(ApiRequest request)
    {
        var sb = new StringBuilder("curl");

        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" -X {request.Method}");
        }

        // Headers
        if (!string.IsNullOrEmpty(request.Headers))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Headers);
                if (headers != null)
                {
                    foreach (var (key, value) in headers)
                    {
                        sb.Append($" -H \"{key}: {value}\"");
                    }
                }
            }
            catch
            {
                // If headers are not valid JSON, skip
            }
        }

        // Body
        if (!string.IsNullOrEmpty(request.Body))
        {
            sb.Append($" -d '{request.Body}'");
        }

        sb.Append($" {request.Url}");

        return sb.ToString();
    }
}
