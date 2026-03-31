using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APIneer.Api.Tests.Proxy;

/// <summary>
/// A lightweight Kestrel-based HTTP server for proxy integration tests.
/// Starts on a random available port and exposes configurable endpoints
/// so the proxy engine can hit a real HTTP target during tests.
/// </summary>
public sealed class TestHttpServer : IAsyncDisposable
{
    private IHost? _host;
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the test server with the given endpoint configuration.
    /// </summary>
    public async Task StartAsync(Action<IEndpointRouteBuilder> configureEndpoints)
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseKestrel(k =>
                {
                    k.Listen(IPAddress.Loopback, 0);
                });
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(configureEndpoints);
                });
            })
            .ConfigureLogging(l => l.ClearProviders())
            .Build();

        await _host.StartAsync();

        var serverAddresses = _host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        BaseUrl = serverAddresses?.Addresses.First()
            ?? throw new InvalidOperationException("Could not determine test server address");
    }

    /// <summary>
    /// Convenience: starts a server with common test endpoints already wired up.
    /// </summary>
    public async Task StartWithDefaults()
    {
        await StartAsync(endpoints =>
        {
            endpoints.MapGet("/echo", (HttpContext ctx) =>
            {
                ctx.Response.Headers["X-Custom-Response"] = "hello";
                return Results.Ok(new { message = "GET echo", method = "GET" });
            });

            endpoints.MapPost("/echo", async (HttpContext ctx) =>
            {
                var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                ctx.Response.Headers["X-Custom-Response"] = "hello";
                return Results.Ok(new { message = "POST echo", method = "POST", receivedBody = body });
            });

            endpoints.MapPut("/echo", async (HttpContext ctx) =>
            {
                var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                return Results.Ok(new { message = "PUT echo", method = "PUT", receivedBody = body });
            });

            endpoints.MapPatch("/echo", async (HttpContext ctx) =>
            {
                var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                return Results.Ok(new { message = "PATCH echo", method = "PATCH", receivedBody = body });
            });

            endpoints.MapDelete("/echo", () =>
                Results.Ok(new { message = "DELETE echo", method = "DELETE" }));

            endpoints.MapMethods("/echo", ["HEAD"], (HttpContext ctx) =>
            {
                ctx.Response.Headers["X-Head-Test"] = "present";
                return Results.Ok();
            });

            endpoints.MapMethods("/echo", ["OPTIONS"], (HttpContext ctx) =>
            {
                ctx.Response.Headers["Allow"] = "GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS";
                return Results.Ok();
            });

            // Echo back request headers in the response body
            endpoints.MapGet("/headers/echo", (HttpContext ctx) =>
            {
                var headers = ctx.Request.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToString());
                return Results.Ok(headers);
            });

            // Return large body
            endpoints.MapGet("/large-body", (HttpContext ctx) =>
            {
                var size = int.Parse(ctx.Request.Query["size"].FirstOrDefault() ?? "1024");
                var data = new string('X', size);
                return Results.Text(data, "text/plain");
            });

            // Slow endpoint (delays before responding)
            endpoints.MapGet("/slow", async (HttpContext ctx) =>
            {
                var delayMs = int.Parse(ctx.Request.Query["ms"].FirstOrDefault() ?? "5000");
                await Task.Delay(delayMs, ctx.RequestAborted);
                return Results.Ok(new { message = "slow response" });
            });

            // Redirect chain
            endpoints.MapGet("/redirect/start", (HttpContext ctx) =>
                Results.Redirect("/redirect/middle"));

            endpoints.MapGet("/redirect/middle", (HttpContext ctx) =>
                Results.Redirect("/redirect/end"));

            endpoints.MapGet("/redirect/end", () =>
                Results.Ok(new { message = "final destination" }));

            // Single redirect
            endpoints.MapGet("/redirect/single", (HttpContext ctx) =>
                Results.Redirect("/echo"));

            // Content-type preservation
            endpoints.MapPost("/content-type/echo", async (HttpContext ctx) =>
            {
                var contentType = ctx.Request.ContentType;
                var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                ctx.Response.ContentType = contentType ?? "application/octet-stream";
                await ctx.Response.WriteAsync(body);
            });

            // Form data endpoint
            endpoints.MapPost("/form", async (HttpContext ctx) =>
            {
                var form = await ctx.Request.ReadFormAsync();
                var result = form.ToDictionary(f => f.Key, f => f.Value.ToString());
                return Results.Ok(result);
            });
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
