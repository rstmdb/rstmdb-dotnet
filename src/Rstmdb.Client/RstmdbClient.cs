using System.Text.Json;
using Rstmdb.Client.Protocol;
using Rstmdb.Client.Transport;

namespace Rstmdb.Client;

/// <summary>
/// The official .NET client for rstmdb - the replicated state machine database.
/// </summary>
public sealed class RstmdbClient : IAsyncDisposable
{
    private readonly Connection _connection;
    private readonly HelloResult _serverInfo;

    private RstmdbClient(Connection connection, HelloResult serverInfo)
    {
        _connection = connection;
        _serverInfo = serverInfo;
    }

    /// <summary>Server information returned from the HELLO handshake.</summary>
    public HelloResult ServerInfo => _serverInfo;

    /// <summary>
    /// Connect to an rstmdb server, perform handshake, and optionally authenticate.
    /// </summary>
    public static async Task<RstmdbClient> ConnectAsync(string host, int port = 7401, RstmdbOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? new RstmdbOptions();
        var conn = await Connection.ConnectAsync(host, port, opts, ct).ConfigureAwait(false);
        try
        {
            var info = await conn.HandshakeAsync(opts, ct).ConfigureAwait(false);
            return new RstmdbClient(conn, info);
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // ── Central dispatch ──────────────────────────────────────────────

    private async Task<T> DoRequestAsync<T>(string op, object? parms, CancellationToken ct)
    {
        var resp = await _connection.SendRequestAsync(op, parms, ct).ConfigureAwait(false);

        if (resp.Status == "error" && resp.Error != null)
        {
            throw new RstmdbException(
                resp.Error.Code,
                resp.Error.Message,
                resp.Error.Retryable,
                resp.Error.Details?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value));
        }

        if (resp.Status != "ok")
            throw new InvalidOperationException($"rstmdb: unexpected status \"{resp.Status}\"");

        if (resp.Result == null)
            return default!;

        return resp.Result.Value.Deserialize<T>()
            ?? throw new InvalidOperationException("rstmdb: null result in ok response");
    }

    private async Task DoRequestVoidAsync(string op, object? parms, CancellationToken ct)
    {
        var resp = await _connection.SendRequestAsync(op, parms, ct).ConfigureAwait(false);

        if (resp.Status == "error" && resp.Error != null)
        {
            throw new RstmdbException(
                resp.Error.Code,
                resp.Error.Message,
                resp.Error.Retryable);
        }
    }

    // ── System ────────────────────────────────────────────────────────

    public async Task PingAsync(CancellationToken ct = default)
    {
        await DoRequestVoidAsync(Operations.Ping, null, ct).ConfigureAwait(false);
    }

    public async Task<ServerInfo> GetInfoAsync(CancellationToken ct = default)
    {
        return await DoRequestAsync<ServerInfo>(Operations.Info, null, ct).ConfigureAwait(false);
    }

    // ── Machines ──────────────────────────────────────────────────────

    public async Task<PutMachineResult> PutMachineAsync(PutMachineRequest request, CancellationToken ct = default)
    {
        return await DoRequestAsync<PutMachineResult>(Operations.PutMachine, request, ct).ConfigureAwait(false);
    }

    public async Task<MachineInfo> GetMachineAsync(string machine, int version, CancellationToken ct = default)
    {
        var parms = new { machine, version };
        return await DoRequestAsync<MachineInfo>(Operations.GetMachine, parms, ct).ConfigureAwait(false);
    }

    public async Task<MachineSummary[]> ListMachinesAsync(CancellationToken ct = default)
    {
        var result = await DoRequestAsync<ListMachinesResult>(Operations.ListMachines, null, ct).ConfigureAwait(false);
        return result.Items;
    }

    // ── Instances ─────────────────────────────────────────────────────

    public async Task<CreateInstanceResult> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct = default)
    {
        return await DoRequestAsync<CreateInstanceResult>(Operations.CreateInstance, request, ct).ConfigureAwait(false);
    }

    public async Task<Instance> GetInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        var parms = new { instance_id = instanceId };
        return await DoRequestAsync<Instance>(Operations.GetInstance, parms, ct).ConfigureAwait(false);
    }

    public async Task<InstanceList> ListInstancesAsync(ListInstancesOptions? options = null, CancellationToken ct = default)
    {
        return await DoRequestAsync<InstanceList>(Operations.ListInstances, options, ct).ConfigureAwait(false);
    }

    public async Task<DeleteInstanceResult> DeleteInstanceAsync(string instanceId, string? idempotencyKey = null, CancellationToken ct = default)
    {
        var parms = new Dictionary<string, object> { ["instance_id"] = instanceId };
        if (idempotencyKey != null)
            parms["idempotency_key"] = idempotencyKey;
        return await DoRequestAsync<DeleteInstanceResult>(Operations.DeleteInstance, parms, ct).ConfigureAwait(false);
    }

    // ── Events ────────────────────────────────────────────────────────

    public async Task<ApplyEventResult> ApplyEventAsync(ApplyEventRequest request, CancellationToken ct = default)
    {
        return await DoRequestAsync<ApplyEventResult>(Operations.ApplyEvent, request, ct).ConfigureAwait(false);
    }

    public async Task<BatchResult[]> BatchAsync(BatchMode mode, BatchOperation[] ops, CancellationToken ct = default)
    {
        var parms = new { mode, ops };
        var result = await DoRequestAsync<BatchResultWrapper>(Operations.Batch, parms, ct).ConfigureAwait(false);
        return result.Results;
    }

    // ── Watch ─────────────────────────────────────────────────────────

    public async Task<Subscription> WatchInstanceAsync(WatchInstanceRequest request, CancellationToken ct = default)
    {
        var sub = new Subscription(_connection);

        var resp = await _connection.SendRequestAsync(Operations.WatchInstance, request, ct).ConfigureAwait(false);

        if (resp.Status == "error" && resp.Error != null)
            throw new RstmdbException(resp.Error.Code, resp.Error.Message, resp.Error.Retryable);

        var result = resp.Result?.Deserialize<SubscriptionIdResult>()
            ?? throw new InvalidOperationException("rstmdb: empty watch result");

        sub.Id = result.SubscriptionId;
        _connection.RegisterSubscription(sub.Id, sub);

        return sub;
    }

    public async Task<Subscription> WatchAllAsync(WatchAllOptions? options = null, CancellationToken ct = default)
    {
        var sub = new Subscription(_connection);

        var resp = await _connection.SendRequestAsync(Operations.WatchAll, options, ct).ConfigureAwait(false);

        if (resp.Status == "error" && resp.Error != null)
            throw new RstmdbException(resp.Error.Code, resp.Error.Message, resp.Error.Retryable);

        var result = resp.Result?.Deserialize<SubscriptionIdResult>()
            ?? throw new InvalidOperationException("rstmdb: empty watch result");

        sub.Id = result.SubscriptionId;
        _connection.RegisterSubscription(sub.Id, sub);

        return sub;
    }

    // ── WAL ───────────────────────────────────────────────────────────

    public async Task<SnapshotResult> SnapshotInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        var parms = new { instance_id = instanceId };
        return await DoRequestAsync<SnapshotResult>(Operations.SnapshotInstance, parms, ct).ConfigureAwait(false);
    }

    public async Task<WalReadResult> WalReadAsync(ulong fromOffset, int? limit = null, CancellationToken ct = default)
    {
        var parms = new Dictionary<string, object> { ["from_offset"] = fromOffset };
        if (limit.HasValue)
            parms["limit"] = limit.Value;
        return await DoRequestAsync<WalReadResult>(Operations.WalRead, parms, ct).ConfigureAwait(false);
    }

    public async Task<WalStatsResult> WalStatsAsync(CancellationToken ct = default)
    {
        return await DoRequestAsync<WalStatsResult>(Operations.WalStats, null, ct).ConfigureAwait(false);
    }

    public async Task<CompactResult> CompactAsync(bool forceSnapshot = false, CancellationToken ct = default)
    {
        var parms = new { force_snapshot = forceSnapshot };
        return await DoRequestAsync<CompactResult>(Operations.Compact, parms, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all instances and machine definitions from the database.
    /// Requires <c>storage.allow_flush_all: true</c> in server configuration.
    /// </summary>
    public async Task<FlushAllResult> FlushAllAsync(CancellationToken ct = default)
    {
        return await DoRequestAsync<FlushAllResult>(Operations.FlushAll, new { }, ct).ConfigureAwait(false);
    }

    // ── Dispose ───────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    // ── Internal helper types ─────────────────────────────────────────

    private sealed class ListMachinesResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("items")]
        public MachineSummary[] Items { get; set; } = Array.Empty<MachineSummary>();
    }

    private sealed class BatchResultWrapper
    {
        [System.Text.Json.Serialization.JsonPropertyName("results")]
        public BatchResult[] Results { get; set; } = Array.Empty<BatchResult>();
    }

    private sealed class SubscriptionIdResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("subscription_id")]
        public string SubscriptionId { get; set; } = "";
    }
}

/// <summary>
/// Options for ListInstances.
/// </summary>
public sealed class ListInstancesOptions
{
    [System.Text.Json.Serialization.JsonPropertyName("machine")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Machine { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("state")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("limit")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("offset")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Offset { get; set; }
}
