using System.Text.Json;
using FluentAssertions;
using APIneer.Api.Models;

namespace APIneer.Api.Tests.Requests;

/// <summary>
/// Unit tests for the ApiRequest model defaults, timestamps, and serialization.
/// These are pure model tests — no HTTP server needed.
/// </summary>
public class RequestModelTests
{
    // ──────────────────────────────────────────────
    // Default Values
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_HaveEmptyGuid_When_IdNotSet()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        request.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Should_HaveZeroSortOrder_When_NotExplicitlySet()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        request.SortOrder.Should().Be(0);
    }

    [Fact]
    public void Should_HaveNullOptionalFields_When_NotSet()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        request.Headers.Should().BeNull();
        request.Body.Should().BeNull();
        request.BodyType.Should().BeNull();
        request.AuthConfig.Should().BeNull();
        request.FolderId.Should().BeNull();
    }

    [Fact]
    public void Should_HaveEmptyHistoryCollection_When_Created()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        request.History.Should().NotBeNull();
        request.History.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // Timestamps
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_HaveDefaultTimestamps_When_NotExplicitlySet()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        request.CreatedAt.Should().Be(default(DateTime));
        request.UpdatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void Should_AcceptExplicitTimestamps_When_Set()
    {
        var now = DateTime.UtcNow;
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com",
            CreatedAt = now,
            UpdatedAt = now
        };

        request.CreatedAt.Should().Be(now);
        request.UpdatedAt.Should().Be(now);
    }

    // ──────────────────────────────────────────────
    // Headers Serialization (JSON key-value pairs)
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_SerializeHeaders_AsJsonKeyValuePairs()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Authorization"] = "Bearer token123",
            ["X-Custom"] = "custom-value"
        };

        var request = new ApiRequest
        {
            Name = "Test",
            Method = "POST",
            Url = "https://example.com",
            Headers = JsonSerializer.Serialize(headers)
        };

        request.Headers.Should().NotBeNullOrEmpty();

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Headers!);
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(3);
        deserialized!["Content-Type"].Should().Be("application/json");
        deserialized["Authorization"].Should().Be("Bearer token123");
        deserialized["X-Custom"].Should().Be("custom-value");
    }

    [Fact]
    public void Should_HandleEmptyHeaders_AsEmptyJsonObject()
    {
        var headers = new Dictionary<string, string>();
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com",
            Headers = JsonSerializer.Serialize(headers)
        };

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Headers!);
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEmpty();
    }

    [Fact]
    public void Should_HandleNullHeaders_Gracefully()
    {
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com",
            Headers = null
        };

        request.Headers.Should().BeNull();
    }

    [Fact]
    public void Should_PreserveHeaderValues_WithSpecialCharacters()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json; charset=utf-8",
            ["Cookie"] = "session=abc123; path=/; HttpOnly",
            ["X-Multi"] = "value1, value2, value3"
        };

        var json = JsonSerializer.Serialize(headers);
        var request = new ApiRequest
        {
            Name = "Test",
            Method = "POST",
            Url = "https://example.com",
            Headers = json
        };

        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Headers!);
        roundTripped!["Content-Type"].Should().Be("application/json; charset=utf-8");
        roundTripped["Cookie"].Should().Contain("HttpOnly");
        roundTripped["X-Multi"].Should().Contain("value2");
    }

    // ──────────────────────────────────────────────
    // RequestHistory Model
    // ──────────────────────────────────────────────

    [Fact]
    public void Should_CreateHistoryEntry_WithRequiredFields()
    {
        var history = new RequestHistory
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            Method = "GET",
            Url = "https://api.example.com/users",
            ResponseStatus = 200,
            ResponseTimeMs = 150,
            ResponseSizeBytes = 1024,
            ExecutedAt = DateTime.UtcNow
        };

        history.Method.Should().Be("GET");
        history.Url.Should().Be("https://api.example.com/users");
        history.ResponseStatus.Should().Be(200);
        history.ResponseTimeMs.Should().Be(150);
        history.ResponseSizeBytes.Should().Be(1024);
    }

    [Fact]
    public void Should_HaveNullOptionalFields_WhenHistoryCreated()
    {
        var history = new RequestHistory
        {
            Method = "GET",
            Url = "https://example.com"
        };

        history.RequestHeaders.Should().BeNull();
        history.RequestBody.Should().BeNull();
        history.ResponseHeaders.Should().BeNull();
        history.ResponseBody.Should().BeNull();
    }
}
