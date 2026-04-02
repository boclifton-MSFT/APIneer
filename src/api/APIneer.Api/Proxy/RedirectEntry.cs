namespace APIneer.Api.Proxy;

/// <summary>
/// A single hop in a redirect chain (e.g., 301 → 302 → 200).
/// </summary>
public record RedirectEntry(string Url, int StatusCode);
