using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using Rstmdb.Client.Protocol;

namespace Rstmdb.Client.Transport;

internal sealed class Connection : IAsyncDisposable
{
    private readonly TcpClient _tcp;
    private SslStream? _ssl;
    private Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WireResponse>> _pending = new();
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
    private long _nextId;
    private readonly CancellationTokenSource _cts = new();
    private Task? _readLoopTask;
    private string _wireMode = "binary_json";
    private readonly TimeSpan _requestTimeout;
    private int _disposed;

    private Connection(TcpClient tcp, Stream stream, TimeSpan requestTimeout)
    {
        _tcp = tcp;
        _stream = stream;
        _requestTimeout = requestTimeout;
    }

    public static async Task<Connection> ConnectAsync(string host, int port, RstmdbOptions opts, CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(opts.ConnectTimeout);
            await tcp.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            tcp.NoDelay = true;

            Stream stream = tcp.GetStream();

            // Optional TLS
            if (opts.Tls != null)
            {
                var sslStream = new SslStream(stream);
                var authOpts = opts.Tls;
                authOpts.TargetHost ??= host;
                await sslStream.AuthenticateAsClientAsync(authOpts, ct).ConfigureAwait(false);
                stream = sslStream;
            }

            var conn = new Connection(tcp, stream, opts.RequestTimeout);
            if (opts.Tls != null)
                conn._ssl = (SslStream)stream;

            return conn;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Performs HELLO (and optionally AUTH) before readLoop starts.
    /// Returns the HelloResult from the server.
    /// </summary>
    public async Task<HelloResult> HandshakeAsync(RstmdbOptions opts, CancellationToken ct)
    {
        // Build HELLO params
        var helloParams = new Dictionary<string, object>
        {
            ["protocol_version"] = 1,
            ["wire_mode"] = opts.WireMode,
        };
        if (!string.IsNullOrEmpty(opts.ClientName))
            helloParams["client_name"] = opts.ClientName;
        if (opts.Features != null && opts.Features.Length > 0)
            helloParams["features"] = opts.Features;

        // Send HELLO directly (before readLoop)
        var helloReq = new WireRequest
        {
            Type = "request",
            Id = NextRequestId(),
            Op = Operations.Hello,
            Params = helloParams,
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(helloReq);
        var frame = FrameCodec.EncodeFrame(payload);
        await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);

        // Read HELLO response directly (binary always for handshake)
        var respPayload = await FrameCodec.DecodeFrameAsync(_stream, ct).ConfigureAwait(false);
        var resp = JsonSerializer.Deserialize<WireResponse>(respPayload)
            ?? throw new InvalidOperationException("rstmdb: empty HELLO response");

        if (resp.Status == "error" && resp.Error != null)
            throw new RstmdbException(resp.Error.Code, resp.Error.Message, resp.Error.Retryable);

        var helloResult = resp.Result?.Deserialize<HelloResult>()
            ?? throw new InvalidOperationException("rstmdb: empty HELLO result");

        // Negotiate wire mode
        _wireMode = helloResult.WireMode;

        // Optional AUTH (in negotiated wire mode)
        if (!string.IsNullOrEmpty(opts.Auth))
        {
            var authParams = new Dictionary<string, object>
            {
                ["method"] = "bearer",
                ["token"] = opts.Auth,
            };
            var authResp = await SendDirectAsync(Operations.Auth, authParams, ct).ConfigureAwait(false);
            if (authResp.Status == "error" && authResp.Error != null)
                throw new RstmdbException(authResp.Error.Code, authResp.Error.Message, authResp.Error.Retryable);
        }

        // Start readLoop
        _readLoopTask = Task.Run(() => ReadLoopAsync(), CancellationToken.None);

        return helloResult;
    }

    private string NextRequestId()
    {
        return Interlocked.Increment(ref _nextId).ToString();
    }

    /// <summary>
    /// Send a request and read response directly (used during handshake before readLoop starts).
    /// </summary>
    private async Task<WireResponse> SendDirectAsync(string op, object? parms, CancellationToken ct)
    {
        var req = new WireRequest
        {
            Type = "request",
            Id = NextRequestId(),
            Op = op,
            Params = parms,
        };

        await WriteMessageAsync(req, ct).ConfigureAwait(false);
        var payload = await ReadMessageAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WireResponse>(payload)
            ?? throw new InvalidOperationException("rstmdb: empty response");
    }

    /// <summary>
    /// Sends a request and waits for the response via the readLoop multiplexer.
    /// </summary>
    public async Task<WireResponse> SendRequestAsync(string op, object? parms, CancellationToken ct)
    {
        var id = NextRequestId();
        var req = new WireRequest
        {
            Type = "request",
            Id = id,
            Op = op,
            Params = parms,
        };

        var tcs = new TaskCompletionSource<WireResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            await WriteMessageAsync(req, ct).ConfigureAwait(false);

            // Await with timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            linkedCts.CancelAfter(_requestTimeout);

            using var reg = linkedCts.Token.Register(() =>
            {
                tcs.TrySetException(
                    ct.IsCancellationRequested
                        ? new OperationCanceledException(ct)
                        : _cts.IsCancellationRequested
                            ? new IOException("rstmdb: connection closed")
                            : new TimeoutException("rstmdb: request timeout"));
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task WriteMessageAsync(WireRequest req, CancellationToken ct)
    {
        byte[] data;
        if (_wireMode == "jsonl")
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(req);
            data = new byte[json.Length + 1];
            json.CopyTo(data, 0);
            data[^1] = (byte)'\n';
        }
        else
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(req);
            data = FrameCodec.EncodeFrame(json);
        }

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(data, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<byte[]> ReadMessageAsync(CancellationToken ct)
    {
        if (_wireMode == "jsonl")
        {
            return await ReadJsonlMessageAsync(ct).ConfigureAwait(false);
        }
        return await FrameCodec.DecodeFrameAsync(_stream, ct).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadJsonlMessageAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            int read = await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("rstmdb: connection closed");
            if (buffer[0] == (byte)'\n')
                break;
            ms.WriteByte(buffer[0]);
        }
        return ms.ToArray();
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                byte[] payload;
                try
                {
                    payload = await ReadMessageAsync(_cts.Token).ConfigureAwait(false);
                }
                catch when (_cts.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Connection error — close everything
                    Close();
                    return;
                }

                var msgType = WireSerializer.PeekType(payload);

                switch (msgType)
                {
                    case "response":
                    {
                        var resp = JsonSerializer.Deserialize<WireResponse>(payload);
                        if (resp != null && _pending.TryRemove(resp.Id, out var tcs))
                        {
                            tcs.TrySetResult(resp);
                        }
                        break;
                    }
                    case "event":
                    {
                        var evt = JsonSerializer.Deserialize<SubscriptionEvent>(payload);
                        if (evt != null && _subscriptions.TryGetValue(evt.SubscriptionId, out var sub))
                        {
                            sub.TryWriteEvent(evt);
                        }
                        break;
                    }
                }
            }
        }
        catch
        {
            Close();
        }
    }

    public void RegisterSubscription(string id, Subscription sub)
    {
        _subscriptions[id] = sub;
    }

    public void RemoveSubscription(string id)
    {
        _subscriptions.TryRemove(id, out _);
    }

    private void Close()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _cts.Cancel();

        // Drain pending with cancellation
        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetException(new IOException("rstmdb: connection closed"));
        }
        _pending.Clear();

        // Close subscriptions
        foreach (var kvp in _subscriptions)
        {
            kvp.Value.CloseFromConnection();
        }
        _subscriptions.Clear();

        try { _ssl?.Dispose(); } catch { }
        try { _tcp.Dispose(); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Best-effort BYE
        try
        {
            using var byeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var byeReq = new WireRequest
            {
                Type = "request",
                Id = NextRequestId(),
                Op = Operations.Bye,
            };
            await WriteMessageAsync(byeReq, byeCts.Token).ConfigureAwait(false);
        }
        catch { }

        _cts.Cancel();

        if (_readLoopTask != null)
        {
            try { await _readLoopTask.ConfigureAwait(false); } catch { }
        }

        // Drain pending
        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetCanceled();
        }
        _pending.Clear();

        // Close subscriptions
        foreach (var kvp in _subscriptions)
        {
            kvp.Value.CloseFromConnection();
        }
        _subscriptions.Clear();

        _ssl?.Dispose();
        _tcp.Dispose();
        _writeLock.Dispose();
        _cts.Dispose();
    }
}
