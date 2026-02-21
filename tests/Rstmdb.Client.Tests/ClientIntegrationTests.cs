using System.Text.Json;
using Xunit;

namespace Rstmdb.Client.Tests;

public class ClientIntegrationTests : IAsyncLifetime
{
    private MockServer _server = null!;

    public async Task InitializeAsync()
    {
        _server = new MockServer();
        _server.Start();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    private Task<RstmdbClient> ConnectAsync()
    {
        return RstmdbClient.ConnectAsync("127.0.0.1", _server.Port);
    }

    [Fact]
    public async Task ConnectAsync_PerformsHandshake()
    {
        await using var client = await ConnectAsync();

        Assert.Equal("rstmdb-mock", client.ServerInfo.ServerName);
        Assert.Equal("0.1.0-test", client.ServerInfo.ServerVersion);
        Assert.Equal(1, client.ServerInfo.ProtocolVersion);
        Assert.Equal("binary_json", client.ServerInfo.WireMode);
    }

    [Fact]
    public async Task PingAsync_Succeeds()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "PING") return new { pong = true };
            return new { };
        };

        await using var client = await ConnectAsync();
        await client.PingAsync();
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsServerInfo()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "INFO")
            {
                return new
                {
                    server_name = "rstmdb",
                    server_version = "0.2.0",
                    protocol_version = 1,
                    features = new[] { "watch", "batch" },
                    max_frame_bytes = 16777216,
                    max_batch_ops = 100,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var info = await client.GetInfoAsync();

        Assert.Equal("rstmdb", info.ServerName);
        Assert.Equal("0.2.0", info.ServerVersion);
        Assert.Equal(16777216, info.MaxFrameBytes);
    }

    [Fact]
    public async Task PutMachineAsync_CreatesNewMachine()
    {
        _server.RequestHandler = (op, parms) =>
        {
            if (op == "PUT_MACHINE")
            {
                return new
                {
                    machine = "order",
                    version = 1,
                    stored_checksum = "abc123",
                    created = true,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.PutMachineAsync(new PutMachineRequest
        {
            Machine = "order",
            Version = 1,
            Definition = new MachineDefinition
            {
                States = new[] { "created", "paid", "shipped" },
                Initial = "created",
                Transitions = new[]
                {
                    new Transition { From = new[] { "created" }, Event = "PAY", To = "paid" },
                    new Transition { From = new[] { "paid" }, Event = "SHIP", To = "shipped" },
                },
            },
        });

        Assert.Equal("order", result.Machine);
        Assert.Equal(1, result.Version);
        Assert.True(result.Created);
        Assert.Equal("abc123", result.StoredChecksum);
    }

    [Fact]
    public async Task GetMachineAsync_ReturnsMachineInfo()
    {
        _server.RequestHandler = (op, parms) =>
        {
            if (op == "GET_MACHINE")
            {
                return new
                {
                    definition = new
                    {
                        states = new[] { "created", "paid" },
                        initial = "created",
                        transitions = new[]
                        {
                            new { from = "created", @event = "PAY", to = "paid" },
                        },
                    },
                    checksum = "sha256abc",
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var info = await client.GetMachineAsync("order", 1);

        Assert.Equal("sha256abc", info.Checksum);
        Assert.Equal("created", info.Definition.Initial);
        Assert.Equal(2, info.Definition.States.Length);
    }

    [Fact]
    public async Task ListMachinesAsync_ReturnsSummaries()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "LIST_MACHINES")
            {
                return new
                {
                    items = new[]
                    {
                        new { machine = "order", versions = new[] { 1, 2 } },
                        new { machine = "payment", versions = new[] { 1 } },
                    },
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var machines = await client.ListMachinesAsync();

        Assert.Equal(2, machines.Length);
        Assert.Equal("order", machines[0].Machine);
        Assert.Equal(2, machines[0].Versions.Length);
    }

    [Fact]
    public async Task CreateInstanceAsync_CreatesNewInstance()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "CREATE_INSTANCE")
            {
                return new
                {
                    instance_id = "order-123",
                    state = "created",
                    wal_offset = 1UL,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.CreateInstanceAsync(new CreateInstanceRequest
        {
            InstanceId = "order-123",
            Machine = "order",
            Version = 1,
        });

        Assert.Equal("order-123", result.InstanceId);
        Assert.Equal("created", result.State);
        Assert.Equal(1UL, result.WalOffset);
    }

    [Fact]
    public async Task GetInstanceAsync_ReturnsInstance()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "GET_INSTANCE")
            {
                return new
                {
                    machine = "order",
                    version = 1,
                    state = "paid",
                    ctx = new { customer = "alice" },
                    last_wal_offset = 5UL,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var instance = await client.GetInstanceAsync("order-123");

        Assert.Equal("order", instance.Machine);
        Assert.Equal("paid", instance.State);
    }

    [Fact]
    public async Task ListInstancesAsync_ReturnsInstanceList()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "LIST_INSTANCES")
            {
                return new
                {
                    instances = new[]
                    {
                        new
                        {
                            id = "order-1",
                            machine = "order",
                            version = 1,
                            state = "created",
                            created_at = 1708510800000L,
                            updated_at = 1708510800000L,
                            last_wal_offset = 1UL,
                        },
                    },
                    total = 1,
                    has_more = false,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var list = await client.ListInstancesAsync();

        Assert.Single(list.Instances);
        Assert.Equal(1, list.Total);
        Assert.False(list.HasMore);
    }

    [Fact]
    public async Task DeleteInstanceAsync_DeletesInstance()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "DELETE_INSTANCE")
            {
                return new
                {
                    instance_id = "order-123",
                    deleted = true,
                    wal_offset = 10UL,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.DeleteInstanceAsync("order-123");

        Assert.True(result.Deleted);
        Assert.Equal("order-123", result.InstanceId);
    }

    [Fact]
    public async Task ApplyEventAsync_TransitionsState()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "APPLY_EVENT")
            {
                return new
                {
                    from_state = "created",
                    to_state = "paid",
                    ctx = new { amount = 99.99 },
                    wal_offset = 2UL,
                    applied = true,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.ApplyEventAsync(new ApplyEventRequest
        {
            InstanceId = "order-123",
            Event = "PAY",
            Payload = new Dictionary<string, object> { ["amount"] = 99.99 },
        });

        Assert.Equal("created", result.FromState);
        Assert.Equal("paid", result.ToState);
        Assert.True(result.Applied);
    }

    [Fact]
    public async Task BatchAsync_ExecutesMultipleOperations()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "BATCH")
            {
                return new
                {
                    results = new[]
                    {
                        new { status = "ok", result = (object?)new { instance_id = "inst-1", state = "created", wal_offset = 1UL } },
                        new { status = "ok", result = (object?)new { from_state = "created", to_state = "paid", wal_offset = 2UL, applied = true } },
                    },
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var results = await client.BatchAsync(BatchMode.Atomic, new[]
        {
            BatchOperation.CreateInstance(new CreateInstanceRequest { Machine = "order", Version = 1 }),
            BatchOperation.ApplyEvent(new ApplyEventRequest { InstanceId = "inst-1", Event = "PAY" }),
        });

        Assert.Equal(2, results.Length);
        Assert.Equal("ok", results[0].Status);
        Assert.Equal("ok", results[1].Status);
    }

    [Fact]
    public async Task SnapshotInstanceAsync_ReturnsSnapshot()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "SNAPSHOT_INSTANCE")
            {
                return new
                {
                    instance_id = "order-123",
                    snapshot_id = "snap-abc",
                    wal_offset = 10UL,
                    size_bytes = 4096L,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.SnapshotInstanceAsync("order-123");

        Assert.Equal("snap-abc", result.SnapshotId);
        Assert.Equal(10UL, result.WalOffset);
    }

    [Fact]
    public async Task WalReadAsync_ReturnsRecords()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "WAL_READ")
            {
                return new
                {
                    records = new[]
                    {
                        new { sequence = 1UL, offset = 0UL, entry = (object?)new { type = "create" } },
                    },
                    next_offset = 1UL,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.WalReadAsync(0, limit: 10);

        Assert.Single(result.Records);
        Assert.Equal(1UL, result.NextOffset);
    }

    [Fact]
    public async Task WalStatsAsync_ReturnsStats()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "WAL_STATS")
            {
                return new
                {
                    entry_count = 1000L,
                    segment_count = 3,
                    total_size_bytes = 1048576L,
                    latest_offset = 999UL,
                    io_stats = new
                    {
                        bytes_written = 500000UL,
                        bytes_read = 200000UL,
                        writes = 100UL,
                        reads = 50UL,
                        fsyncs = 100UL,
                    },
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var stats = await client.WalStatsAsync();

        Assert.Equal(1000, stats.EntryCount);
        Assert.Equal(3, stats.SegmentCount);
        Assert.Equal(100UL, stats.IoStats.Fsyncs);
    }

    [Fact]
    public async Task CompactAsync_ReturnsResult()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "COMPACT")
            {
                return new
                {
                    snapshots_created = 2,
                    segments_deleted = 5,
                    bytes_reclaimed = 1048576L,
                    total_snapshots = 10,
                    wal_segments = 3,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var result = await client.CompactAsync(forceSnapshot: true);

        Assert.Equal(2, result.SnapshotsCreated);
        Assert.Equal(5, result.SegmentsDeleted);
    }

    [Fact]
    public async Task ErrorCode_MapsToRstmdbException()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "GET_INSTANCE")
            {
                return new MockError("INSTANCE_NOT_FOUND", "instance not found");
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var ex = await Assert.ThrowsAsync<RstmdbException>(
            () => client.GetInstanceAsync("nonexistent"));

        Assert.Equal(ErrorCodes.InstanceNotFound, ex.ErrorCode);
        Assert.True(RstmdbException.IsInstanceNotFound(ex));
        Assert.False(RstmdbException.IsNotFound(ex));
    }

    [Fact]
    public async Task ErrorCode_InvalidTransition()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "APPLY_EVENT")
            {
                return new MockError("INVALID_TRANSITION", "cannot apply PAY in state shipped");
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var ex = await Assert.ThrowsAsync<RstmdbException>(
            () => client.ApplyEventAsync(new ApplyEventRequest
            {
                InstanceId = "order-123",
                Event = "PAY",
            }));

        Assert.True(RstmdbException.IsInvalidTransition(ex));
    }

    [Fact]
    public async Task ErrorCode_RetryableError()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "PING")
            {
                return new MockError("RATE_LIMITED", "too many requests", retryable: true);
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var ex = await Assert.ThrowsAsync<RstmdbException>(
            () => client.PingAsync());

        Assert.True(ex.IsRetryable);
        Assert.True(RstmdbException.CheckRetryable(ex));
    }

    [Fact]
    public async Task CancellationToken_CancelsRequest()
    {
        _server.RequestHandler = (op, _) =>
        {
            // Simulate slow response
            Thread.Sleep(5000);
            return new { pong = true };
        };

        await using var client = await ConnectAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PingAsync(cts.Token));
    }

    [Fact]
    public async Task WatchInstanceAsync_ReceivesEvents()
    {
        string? capturedSubId = null;
        _server.RequestHandler = (op, parms) =>
        {
            if (op == "WATCH_INSTANCE")
            {
                capturedSubId = "sub-123";
                return new { subscription_id = capturedSubId };
            }
            if (op == "UNWATCH")
            {
                return new { };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var sub = await client.WatchInstanceAsync(new WatchInstanceRequest
        {
            InstanceId = "order-123",
            IncludeCtx = true,
        });

        Assert.Equal("sub-123", sub.Id);

        // Push an event from the server
        await _server.SendEventAsync("sub-123", new
        {
            instance_id = "order-123",
            machine = "order",
            version = 1,
            wal_offset = 10UL,
            from_state = "created",
            to_state = "paid",
            @event = "PAY",
        });

        // Read the event
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub.Events.ReadAsync(cts.Token);

        Assert.Equal("order-123", received.InstanceId);
        Assert.Equal("created", received.FromState);
        Assert.Equal("paid", received.ToState);

        await sub.DisposeAsync();
    }

    [Fact]
    public async Task WatchAllAsync_ReceivesEvents()
    {
        _server.RequestHandler = (op, _) =>
        {
            if (op == "WATCH_ALL")
            {
                return new { subscription_id = "sub-all" };
            }
            if (op == "UNWATCH")
            {
                return new { };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var sub = await client.WatchAllAsync(new WatchAllOptions
        {
            IncludeCtx = true,
            Machines = new[] { "order" },
        });

        Assert.Equal("sub-all", sub.Id);

        await _server.SendEventAsync("sub-all", new
        {
            instance_id = "order-456",
            machine = "order",
            version = 1,
            wal_offset = 20UL,
            from_state = "paid",
            to_state = "shipped",
            @event = "SHIP",
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub.Events.ReadAsync(cts.Token);

        Assert.Equal("order-456", received.InstanceId);
        Assert.Equal("SHIP", received.Event);

        await sub.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_SendsByeAndCleanup()
    {
        _server.RequestHandler = (op, _) => new { };

        await using var client = await ConnectAsync();
        await client.PingAsync();
        // DisposeAsync called by await using — should not throw
    }

    [Fact]
    public async Task ConnectAsync_WithAuth()
    {
        _server.RequestHandler = (op, _) => new { pong = true };

        await using var client = await RstmdbClient.ConnectAsync("127.0.0.1", _server.Port,
            new RstmdbOptions { Auth = "test-token" });

        await client.PingAsync();
    }

    [Fact]
    public async Task ListInstancesAsync_WithOptions()
    {
        string? receivedMachine = null;
        _server.RequestHandler = (op, parms) =>
        {
            if (op == "LIST_INSTANCES" && parms.HasValue)
            {
                if (parms.Value.TryGetProperty("machine", out var m))
                    receivedMachine = m.GetString();

                return new
                {
                    instances = Array.Empty<object>(),
                    total = 0,
                    has_more = false,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        var list = await client.ListInstancesAsync(new ListInstancesOptions
        {
            Machine = "order",
            Limit = 10,
        });

        Assert.Equal("order", receivedMachine);
        Assert.Equal(0, list.Total);
    }

    [Fact]
    public async Task DeleteInstanceAsync_WithIdempotencyKey()
    {
        string? receivedKey = null;
        _server.RequestHandler = (op, parms) =>
        {
            if (op == "DELETE_INSTANCE" && parms.HasValue)
            {
                if (parms.Value.TryGetProperty("idempotency_key", out var k))
                    receivedKey = k.GetString();

                return new
                {
                    instance_id = "order-123",
                    deleted = true,
                    wal_offset = 10UL,
                };
            }
            return new { };
        };

        await using var client = await ConnectAsync();
        await client.DeleteInstanceAsync("order-123", idempotencyKey: "key-1");

        Assert.Equal("key-1", receivedKey);
    }
}
