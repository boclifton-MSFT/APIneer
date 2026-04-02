using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace APIneer.Api.Mcp;

public class StdioMcpTransport : IMcpTransport
{
    private readonly Process _process;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger? _logger;
    private Task? _readTask;
    private bool _disposed;

    public StdioMcpTransport(string command, string[]? args = null,
        Dictionary<string, string>? environmentVariables = null, ILogger? logger = null)
    {
        _logger = logger;

        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (args is { Length: > 0 })
        {
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);
        }

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
                psi.Environment[key] = value;
        }

        _process = new Process { StartInfo = psi };
        _process.Start();

        _readTask = Task.Run(() => ReadLoop(_cts.Token));

        // Log stderr (don't treat as errors)
        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var line = await _process.StandardError.ReadLineAsync(_cts.Token);
                    if (line is null) break;
                    _logger?.LogDebug("[MCP stderr] {Line}", line);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "MCP stderr read error");
            }
        });
    }

    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_process.HasExited)
            throw new InvalidOperationException($"MCP server process has exited with code {_process.ExitCode}");

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.Id] = tcs;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(30));
        await using var reg = linkedCts.Token.Register(() =>
        {
            if (_pending.TryRemove(request.Id, out var removed))
                removed.TrySetCanceled(linkedCts.Token);
        });

        await WriteJsonLine(request, ct);

        return await tcs.Task;
    }

    public async Task SendNotificationAsync(string method, object? @params = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var notification = new JsonRpcNotification { Method = method, Params = @params };
        await WriteJsonLine(notification, ct);
    }

    private async Task WriteJsonLine<T>(T message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _writeLock.WaitAsync(ct);
        try
        {
            await _process.StandardInput.WriteLineAsync(json.AsMemory(), ct);
            await _process.StandardInput.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync(ct);
                if (line is null) break;

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var response = JsonSerializer.Deserialize<JsonRpcResponse>(line);
                    if (response is not null && _pending.TryRemove(response.Id, out var tcs))
                    {
                        tcs.TrySetResult(response);
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse MCP response: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MCP stdout read loop failed");
        }
        finally
        {
            // Fail all pending requests
            foreach (var kvp in _pending)
            {
                if (_pending.TryRemove(kvp.Key, out var tcs))
                    tcs.TrySetException(new InvalidOperationException("MCP server process ended"));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _cts.CancelAsync();

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(3000))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch { /* best effort */ }

        if (_readTask is not null)
        {
            try { await _readTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* best effort */ }
        }

        _process.Dispose();
        _cts.Dispose();
        _writeLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
