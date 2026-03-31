namespace APIneer.Api.Proxy;

/// <summary>
/// A single hop in a redirect chain (e.g., 301 → 302 → 200).
/// </summary>
public class RedirectEntry
{
    public required string Url { get; set; }
    public int StatusCode { get; set; }
}
