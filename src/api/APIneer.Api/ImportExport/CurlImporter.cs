using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace APIneer.Api.ImportExport;

/// <summary>
/// Parses cURL commands and extracts method, URL, headers, body, and auth info.
/// </summary>
public static partial class CurlImporter
{
    public static CurlParseResult Parse(string curlCommand)
    {
        if (string.IsNullOrWhiteSpace(curlCommand))
            throw new ImportValidationException("cURL command is empty.");

        // Normalize multiline (backslash continuation)
        var normalized = BackslashContinuationRegex().Replace(curlCommand.Trim(), " ");
        normalized = normalized.Trim();

        if (!normalized.StartsWith("curl", StringComparison.OrdinalIgnoreCase))
            throw new ImportValidationException("Not a valid cURL command.");

        var tokens = Tokenize(normalized);

        string? method = null;
        string? url = null;
        string? body = null;
        var headers = new Dictionary<string, string>();

        int i = 1; // Skip "curl"
        while (i < tokens.Count)
        {
            var token = tokens[i];

            switch (token)
            {
                case "-X" when i + 1 < tokens.Count:
                    method = tokens[++i].ToUpperInvariant();
                    break;

                case "-H" when i + 1 < tokens.Count:
                {
                    var headerVal = tokens[++i];
                    var colonIdx = headerVal.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = headerVal[..colonIdx].Trim();
                        var value = headerVal[(colonIdx + 1)..].Trim();
                        headers[key] = value;
                    }
                    break;
                }

                case "-d" or "--data" when i + 1 < tokens.Count:
                    body = tokens[++i];
                    break;

                case "-u" when i + 1 < tokens.Count:
                {
                    var credentials = tokens[++i];
                    var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
                    headers["Authorization"] = $"Basic {base64}";
                    break;
                }

                default:
                    // If it looks like a URL (starts with http/https or contains ://)
                    if (url is null && !token.StartsWith("-") &&
                        (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         token.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         token.Contains("://")))
                    {
                        url = token;
                    }
                    break;
            }

            i++;
        }

        method ??= body != null ? "POST" : "GET";
        url ??= "";

        var headersJson = headers.Count > 0
            ? JsonSerializer.Serialize(headers)
            : null;

        // Generate a name from the URL
        var name = GenerateName(url, method);

        return new CurlParseResult(name, method, url, headersJson, body);
    }

    private static string GenerateName(string url, string method)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimEnd('/');
            var lastSegment = path.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s)) ?? "request";
            return $"{method} {lastSegment}";
        }
        catch
        {
            return $"{method} request";
        }
    }

    /// <summary>
    /// Tokenizes a cURL command respecting single and double quotes.
    /// </summary>
    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char? inQuote = null;

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (inQuote.HasValue)
            {
                if (c == inQuote.Value)
                {
                    inQuote = null; // End of quoted section
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '\'' || c == '"')
                {
                    inQuote = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}

public partial class CurlImporter
{
    [GeneratedRegex(@"\\\s*\n\s*")]
    private static partial Regex BackslashContinuationRegex();
}

public record CurlParseResult(string Name, string Method, string Url, string? Headers, string? Body);
