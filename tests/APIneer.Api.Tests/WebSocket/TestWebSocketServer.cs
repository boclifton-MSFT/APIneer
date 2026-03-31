using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APIneer.Api.Tests.WebSocket;

/// <summary>
/// A lightweight Kestrel-based WebSocket server for integration tests.
/// Accepts WebSocket connections and echoes back messages.
/// </summary>
public sealed class TestWebSocketServer : IAsyncDisposable
{
    private IHost? _host;
    public string BaseUrl { get; private set; } = string.Empty;
    public string WsUrl => BaseUrl.Replace("http://", "ws://").Replace("https://", "wss://");

    /// <summary>
    /// Starts the test server with a WebSocket echo endpoint.
    /// </summary>
    public async Task StartAsync()
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
                    app.UseWebSockets();
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path == "/echo" && context.WebSockets.IsWebSocketRequest)
                        {
                            using var ws = await context.WebSockets.AcceptWebSocketAsync();
                            await EchoLoop(ws, context.RequestAborted);
                        }
                        else if (context.Request.Path == "/close-immediately" && context.WebSockets.IsWebSocketRequest)
                        {
                            using var ws = await context.WebSockets.AcceptWebSocketAsync();
                            await ws.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Server closing",
                                CancellationToken.None);
                        }
                        else
                        {
                            await next();
                        }
                    });
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

    private static async Task EchoLoop(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Echo server closing",
                        CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var echoMsg = $"echo: {msg}";
                    var echoBytes = Encoding.UTF8.GetBytes(echoMsg);

                    await ws.SendAsync(
                        new ArraySegment<byte>(echoBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        ct);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
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
