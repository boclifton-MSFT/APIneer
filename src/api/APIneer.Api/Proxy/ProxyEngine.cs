using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace APIneer.Api.Proxy;

/// <summary>
/// HTTP proxy engine that sends requests to target APIs on behalf of the user.
/// Never throws for transport-level errors; returns a structured <see cref="ProxyError"/> instead.
/// Uses IHttpClientFactory for connection pooling to avoid socket exhaustion.
/// </summary>
public sealed class ProxyEngine(IHttpClientFactory httpClientFactory) : IProxyEngine
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxRequestBodyBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxRedirects = 20;
    private const int LargeResponseThresholdBytes = 1 * 1024 * 1024; // 1 MB

    /// <inheritdoc />
    public async Task<ProxyResponse> SendAsync(ProxyRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Url) ||
                !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                stopwatch.Stop();
                return CreateErrorResponse(stopwatch, "INVALID_URL",
                    $"The URL '{request.Url}' is not a valid HTTP or HTTPS URL.");
            }

            if (request.Body is not null && Encoding.UTF8.GetByteCount(request.Body) > MaxRequestBodyBytes)
            {
                stopwatch.Stop();
                return CreateErrorResponse(stopwatch, "REQUEST_ERROR",
                    "Request body exceeds the 10 MB size limit.");
            }

            var timeout = TimeSpan.FromSeconds(
                Math.Clamp(request.TimeoutSeconds ?? DefaultTimeoutSeconds, 1, 300));

            var httpClient = httpClientFactory.CreateClient("ProxyEngine");
            httpClient.Timeout = timeout;

            return await ExecuteWithRedirectsAsync(httpClient, request, stopwatch, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return CreateErrorResponse(stopwatch, "TIMEOUT",
                $"The request timed out after {request.TimeoutSeconds ?? DefaultTimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex) when (IsConnectionRefused(ex))
        {
            stopwatch.Stop();
            return CreateErrorResponse(stopwatch, "CONNECTION_REFUSED",
                $"Connection refused by the target host: {ex.Message}");
        }
        catch (HttpRequestException ex) when (IsDnsFailure(ex))
        {
            stopwatch.Stop();
            return CreateErrorResponse(stopwatch, "DNS_FAILURE",
                $"DNS resolution failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse(stopwatch, "REQUEST_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// Core execution loop that manually follows redirects so we can capture the chain when requested.
    /// </summary>
    private static async Task<ProxyResponse> ExecuteWithRedirectsAsync(
        HttpClient httpClient,
        ProxyRequest request,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var redirectChain = request.CaptureRedirectChain ? new List<RedirectEntry>() : null;
        var currentUrl = request.Url;
        var currentMethod = new HttpMethod(request.Method);
        var isFirstRequest = true;
        var redirectCount = 0;

        HttpResponseMessage? response = null;

        try
        {
            while (true)
            {
                using var httpRequest = BuildHttpRequest(currentMethod, currentUrl, request, isFirstRequest);
                response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                var statusCode = (int)response.StatusCode;
                var isRedirect = statusCode is >= 300 and <= 399 && response.Headers.Location is not null;

                if (request.FollowRedirects && isRedirect && redirectCount < MaxRedirects)
                {
                    redirectChain?.Add(new RedirectEntry
                    {
                        Url = currentUrl,
                        StatusCode = statusCode
                    });

                    var location = response.Headers.Location!;
                    currentUrl = location.IsAbsoluteUri
                        ? location.ToString()
                        : new Uri(new Uri(currentUrl), location).ToString();

                    // 301/302/303 convert to GET per HTTP spec; 307/308 preserve method
                    if (statusCode is 301 or 302 or 303)
                    {
                        currentMethod = HttpMethod.Get;
                    }

                    isFirstRequest = false;
                    redirectCount++;
                    response.Dispose();
                    response = null;
                    continue;
                }

                break;
            }

            stopwatch.Stop();

            string? body;
            if (request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            {
                body = null;
            }
            else
            {
                // Stream large responses to avoid excessive memory allocation
                var contentLength = response!.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > LargeResponseThresholdBytes)
                {
                    // Read up to limit + 1 byte to detect truncation
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var buffer = new char[LargeResponseThresholdBytes];
                    var charsRead = await reader.ReadBlockAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    body = new string(buffer, 0, charsRead);
                }
                else
                {
                    body = await response!.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            var responseHeaders = CaptureResponseHeaders(response!);
            var responseSizeBytes = CalculateResponseSize(body, responseHeaders);

            return new ProxyResponse
            {
                StatusCode = (int)response!.StatusCode,
                Headers = responseHeaders,
                Body = body,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseSizeBytes = responseSizeBytes,
                RedirectChain = redirectChain
            };
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <summary>
    /// Builds an <see cref="HttpRequestMessage"/> from the proxy request model.
    /// Only includes the body on the first request (redirected 301/302/303 requests become GET without body).
    /// </summary>
    private static HttpRequestMessage BuildHttpRequest(
        HttpMethod method, string url, ProxyRequest proxyRequest, bool includeBody)
    {
        var httpRequest = new HttpRequestMessage(method, url);

        foreach (var (key, value) in proxyRequest.Headers)
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue; // Content-Type is set on the content object
            httpRequest.Headers.TryAddWithoutValidation(key, value);
        }

        if (includeBody && proxyRequest.Body is not null)
        {
            var contentType = proxyRequest.Headers
                .FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value ?? proxyRequest.BodyType ?? "text/plain";

            httpRequest.Content = new StringContent(proxyRequest.Body, Encoding.UTF8, contentType);
        }

        return httpRequest;
    }

    /// <summary>
    /// Captures both response headers and content headers into a single dictionary.
    /// </summary>
    private static Dictionary<string, IEnumerable<string>> CaptureResponseHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in response.Headers)
        {
            headers[key] = values.ToList();
        }

        if (response.Content is not null)
        {
            foreach (var (key, values) in response.Content.Headers)
            {
                headers[key] = values.ToList();
            }
        }

        return headers;
    }

    /// <summary>
    /// Calculates total response size in bytes (body + serialised header pairs).
    /// </summary>
    private static long CalculateResponseSize(string? body, Dictionary<string, IEnumerable<string>> headers)
    {
        long size = 0;

        if (body is not null)
        {
            size += Encoding.UTF8.GetByteCount(body);
        }

        foreach (var (key, values) in headers)
        {
            foreach (var value in values)
            {
                // "Key: Value\r\n"
                size += Encoding.UTF8.GetByteCount(key) + 2 + Encoding.UTF8.GetByteCount(value) + 2;
            }
        }

        return size;
    }

    private static bool IsConnectionRefused(HttpRequestException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
                return true;
            inner = inner.InnerException;
        }

        return false;
    }

    private static bool IsDnsFailure(HttpRequestException ex)
    {
        // .NET 7+ exposes HttpRequestError for reliable classification
        if (ex.HttpRequestError == System.Net.Http.HttpRequestError.NameResolutionError)
            return true;

        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner is SocketException se &&
                se.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
                return true;
            inner = inner.InnerException;
        }

        return false;
    }

    private static ProxyResponse CreateErrorResponse(Stopwatch stopwatch, string code, string message) =>
        new()
        {
            StatusCode = 0,
            ResponseTimeMs = stopwatch.ElapsedMilliseconds,
            Error = new ProxyError { Code = code, Message = message }
        };
}
