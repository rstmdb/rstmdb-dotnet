# Rstmdb.Client

Official .NET client for [rstmdb](https://github.com/rstmdb/rstmdb) — the replicated state machine database.

[![CI](https://github.com/rstmdb/rstmdb-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/rstmdb/rstmdb-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Rstmdb.Client.svg)](https://www.nuget.org/packages/Rstmdb.Client)

## Installation

```bash
dotnet add package Rstmdb.Client
```

## Quick Start

```csharp
using Rstmdb.Client;

// Connect
await using var client = await RstmdbClient.ConnectAsync("127.0.0.1", 7401);

// Define a state machine
await client.PutMachineAsync(new PutMachineRequest
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

// Create an instance
var inst = await client.CreateInstanceAsync(new CreateInstanceRequest
{
    Machine = "order",
    Version = 1,
});

// Transition state
var result = await client.ApplyEventAsync(new ApplyEventRequest
{
    InstanceId = inst.InstanceId,
    Event = "PAY",
});
Console.WriteLine($"{result.FromState} -> {result.ToState}"); // created -> paid
```

## API Overview

### Connection

```csharp
// Plain TCP
await using var client = await RstmdbClient.ConnectAsync("host", 7401);

// With authentication
await using var client = await RstmdbClient.ConnectAsync("host", 7401,
    new RstmdbOptions { Auth = "my-token" });

// With TLS
await using var client = await RstmdbClient.ConnectAsync("host", 7401,
    new RstmdbOptions { Tls = RstmdbOptions.InsecureTls() });
```

### Machines

```csharp
await client.PutMachineAsync(request);
var machine = await client.GetMachineAsync("order", version: 1);
var machines = await client.ListMachinesAsync();
```

### Instances

```csharp
var created = await client.CreateInstanceAsync(request);
var instance = await client.GetInstanceAsync("instance-id");
var list = await client.ListInstancesAsync(new ListInstancesOptions { Machine = "order", Limit = 10 });
var deleted = await client.DeleteInstanceAsync("instance-id");
```

### Events

```csharp
var result = await client.ApplyEventAsync(new ApplyEventRequest
{
    InstanceId = "instance-id",
    Event = "PAY",
    Payload = new Dictionary<string, object> { ["amount"] = 99.99 },
});
```

### Batch Operations

```csharp
var results = await client.BatchAsync(BatchMode.Atomic, new[]
{
    BatchOperation.CreateInstance(new CreateInstanceRequest { Machine = "order", Version = 1 }),
    BatchOperation.ApplyEvent(new ApplyEventRequest { InstanceId = "id", Event = "PAY" }),
});
```

### Watch (Streaming)

```csharp
// Watch a specific instance
await using var sub = await client.WatchInstanceAsync(new WatchInstanceRequest
{
    InstanceId = "order-123",
    IncludeCtx = true,
});

await foreach (var evt in sub.ReadAllAsync(cancellationToken))
{
    Console.WriteLine($"{evt.FromState} -> {evt.ToState} via {evt.Event}");
}

// Watch all events
await using var sub = await client.WatchAllAsync(new WatchAllOptions
{
    Machines = new[] { "order" },
    IncludeCtx = true,
});
```

### WAL Operations

```csharp
var snapshot = await client.SnapshotInstanceAsync("instance-id");
var walData = await client.WalReadAsync(fromOffset: 0, limit: 100);
var stats = await client.WalStatsAsync();
var compact = await client.CompactAsync(forceSnapshot: true);
```

### Error Handling

```csharp
try
{
    await client.GetInstanceAsync("nonexistent");
}
catch (RstmdbException ex) when (RstmdbException.IsInstanceNotFound(ex))
{
    Console.WriteLine("Instance not found");
}
catch (RstmdbException ex) when (ex.IsRetryable)
{
    // Safe to retry
}
```

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `Auth` | `null` | Bearer token for authentication |
| `Tls` | `null` | TLS options (`null` = plain TCP) |
| `ConnectTimeout` | `10s` | TCP connection timeout |
| `RequestTimeout` | `30s` | Per-request timeout |
| `ClientName` | `null` | Client identifier sent in handshake |
| `WireMode` | `"binary_json"` | Wire protocol mode |

## License

MIT
