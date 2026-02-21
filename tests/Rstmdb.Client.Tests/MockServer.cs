using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Rstmdb.Client.Protocol;

namespace Rstmdb.Client.Tests;

/// <summary>
/// TCP mock server that implements RCPX binary framing for testing.
/// </summary>
internal sealed class MockServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;

    /// <summary>Handler invoked for each request. Receives (op, params) and returns the result object.</summary>
    public Func<string, JsonElement?, object?>? RequestHandler { get; set; }

    /// <summary>Port the server is listening on.</summary>
    public int Port { get; }

    public MockServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// Start accepting one client connection and dispatching requests.
    /// </summary>
    public void Start()
    {
        _acceptTask = Task.Run(async () =>
        {
            try
            {
                _client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _client.NoDelay = true;
                _stream = _client.GetStream();
                await DispatchLoopAsync().ConfigureAwait(false);
            }
            catch when (_cts.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception)
            {
                // Connection closed
            }
        });
    }

    private async Task DispatchLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            byte[] payload;
            try
            {
                payload = await DecodeFrameAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            var msg = JsonSerializer.Deserialize<JsonElement>(payload);
            var type = msg.GetProperty("type").GetString();
            var id = msg.GetProperty("id").GetString()!;
            var op = msg.GetProperty("op").GetString()!;

            JsonElement? parms = null;
            if (msg.TryGetProperty("params", out var p))
                parms = p;

            if (op == "HELLO")
            {
                await SendResponseAsync(id, new
                {
                    protocol_version = 1,
                    wire_mode = "binary_json",
                    server_name = "rstmdb-mock",
                    server_version = "0.1.0-test",
                    features = new[] { "watch", "batch" },
                }).ConfigureAwait(false);
                continue;
            }

            if (op == "AUTH")
            {
                await SendResponseAsync(id, new { authenticated = true }).ConfigureAwait(false);
                continue;
            }

            if (op == "BYE")
            {
                return;
            }

            // Dispatch to handler
            if (RequestHandler != null)
            {
                try
                {
                    var result = RequestHandler(op, parms);
                    if (result is MockError mockErr)
                    {
                        await SendErrorResponseAsync(id, mockErr.Code, mockErr.Message, mockErr.Retryable).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendResponseAsync(id, result).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    await SendErrorResponseAsync(id, "INTERNAL_ERROR", ex.Message, false).ConfigureAwait(false);
                }
            }
            else
            {
                await SendResponseAsync(id, new { }).ConfigureAwait(false);
            }
        }
    }

    public async Task SendResponseAsync(string id, object? result)
    {
        var resp = new
        {
            type = "response",
            id,
            status = "ok",
            result,
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(resp);
        var frame = FrameCodec.EncodeFrame(json);
        await _stream!.WriteAsync(frame).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
    }

    public async Task SendErrorResponseAsync(string id, string code, string message, bool retryable)
    {
        var resp = new
        {
            type = "response",
            id,
            status = "error",
            error = new { code, message, retryable },
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(resp);
        var frame = FrameCodec.EncodeFrame(json);
        await _stream!.WriteAsync(frame).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Push an event to the connected client (for watch tests).
    /// </summary>
    public async Task SendEventAsync(string subscriptionId, object eventData)
    {
        var evt = new Dictionary<string, object>
        {
            ["type"] = "event",
            ["subscription_id"] = subscriptionId,
        };

        // Merge eventData properties
        var json = JsonSerializer.Serialize(eventData);
        var props = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
        foreach (var kvp in props)
        {
            evt[kvp.Key] = kvp.Value;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(evt);
        var frame = FrameCodec.EncodeFrame(payload);
        await _stream!.WriteAsync(frame).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
    }

    private async Task<byte[]> DecodeFrameAsync()
    {
        var header = new byte[FrameCodec.HeaderSize];
        await ReadExactlyAsync(header, FrameCodec.HeaderSize).ConfigureAwait(false);

        var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(10, 4));
        var payload = new byte[payloadLen];
        if (payloadLen > 0)
        {
            await ReadExactlyAsync(payload, (int)payloadLen).ConfigureAwait(false);
        }
        return payload;
    }

    private async Task ReadExactlyAsync(byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await _stream!.ReadAsync(buffer.AsMemory(offset, count - offset)).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("Connection closed");
            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        _client?.Dispose();
        if (_acceptTask != null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }
}

/// <summary>
/// Return this from a RequestHandler to make the mock server send an error response.
/// </summary>
internal sealed class MockError
{
    public string Code { get; }
    public string Message { get; }
    public bool Retryable { get; }

    public MockError(string code, string message, bool retryable = false)
    {
        Code = code;
        Message = message;
        Retryable = retryable;
    }
}
