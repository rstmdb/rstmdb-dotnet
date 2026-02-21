using Rstmdb.Client;

// Graceful shutdown via Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nShutting down...");
};

await using var client = await RstmdbClient.ConnectAsync("127.0.0.1", 7401);
Console.WriteLine("Connected. Watching all events (Ctrl+C to stop)...");

// Subscribe to all events with context included
await using var sub = await client.WatchAllAsync(new WatchAllOptions
{
    IncludeCtx = true,
});

Console.WriteLine($"Subscription ID: {sub.Id}");

// Stream events using IAsyncEnumerable
try
{
    await foreach (var evt in sub.ReadAllAsync(cts.Token))
    {
        Console.WriteLine($"[offset={evt.WalOffset}] {evt.Machine}/{evt.InstanceId}: " +
                          $"{evt.FromState} --{evt.Event}--> {evt.ToState}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped watching.");
}
