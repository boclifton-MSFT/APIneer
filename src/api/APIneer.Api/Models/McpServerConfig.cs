namespace APIneer.Api.Models;

public class McpServerConfig
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string TransportType { get; set; } // "stdio" or "streamable-http"
    public string? Command { get; set; }
    public string? Args { get; set; } // JSON array
    public string? EnvironmentVariables { get; set; } // JSON object
    public string? Headers { get; set; } // JSON object of custom headers
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
