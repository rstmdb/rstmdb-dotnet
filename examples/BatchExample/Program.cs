using Rstmdb.Client;

await using var client = await RstmdbClient.ConnectAsync("127.0.0.1", 7401);
Console.WriteLine("Connected.");

// Atomic batch: all operations succeed or all fail
Console.WriteLine("\n--- Atomic Batch ---");
var atomicResults = await client.BatchAsync(BatchMode.Atomic, new[]
{
    BatchOperation.CreateInstance(new CreateInstanceRequest
    {
        InstanceId = "batch-1",
        Machine = "order",
        Version = 1,
    }),
    BatchOperation.CreateInstance(new CreateInstanceRequest
    {
        InstanceId = "batch-2",
        Machine = "order",
        Version = 1,
    }),
});

for (int i = 0; i < atomicResults.Length; i++)
{
    Console.WriteLine($"  Op {i}: status={atomicResults[i].Status}");
}

// Best-effort batch: each operation independent
Console.WriteLine("\n--- Best-Effort Batch ---");
var bestEffortResults = await client.BatchAsync(BatchMode.BestEffort, new[]
{
    BatchOperation.ApplyEvent(new ApplyEventRequest
    {
        InstanceId = "batch-1",
        Event = "PAY",
        Payload = new Dictionary<string, object> { ["amount"] = 50.00 },
    }),
    BatchOperation.ApplyEvent(new ApplyEventRequest
    {
        InstanceId = "batch-2",
        Event = "PAY",
        Payload = new Dictionary<string, object> { ["amount"] = 75.00 },
    }),
    BatchOperation.DeleteInstance("batch-1"),
});

for (int i = 0; i < bestEffortResults.Length; i++)
{
    var r = bestEffortResults[i];
    if (r.Error != null)
        Console.WriteLine($"  Op {i}: ERROR {r.Error.Code}: {r.Error.Message}");
    else
        Console.WriteLine($"  Op {i}: status={r.Status}");
}
