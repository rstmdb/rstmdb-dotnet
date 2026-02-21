using Rstmdb.Client;

// Connect to rstmdb
await using var client = await RstmdbClient.ConnectAsync("127.0.0.1", 7401);
Console.WriteLine($"Connected to {client.ServerInfo.ServerName} v{client.ServerInfo.ServerVersion}");

// Define a state machine
var putResult = await client.PutMachineAsync(new PutMachineRequest
{
    Machine = "order",
    Version = 1,
    Definition = new MachineDefinition
    {
        States = new[] { "created", "paid", "shipped", "delivered" },
        Initial = "created",
        Transitions = new[]
        {
            new Transition { From = new[] { "created" }, Event = "PAY", To = "paid" },
            new Transition { From = new[] { "paid" }, Event = "SHIP", To = "shipped" },
            new Transition { From = new[] { "shipped" }, Event = "DELIVER", To = "delivered" },
        },
    },
});
Console.WriteLine($"Machine '{putResult.Machine}' v{putResult.Version} created={putResult.Created}");

// Create an instance
var createResult = await client.CreateInstanceAsync(new CreateInstanceRequest
{
    Machine = "order",
    Version = 1,
    InitialCtx = new Dictionary<string, object> { ["customer"] = "alice" },
});
Console.WriteLine($"Instance '{createResult.InstanceId}' in state '{createResult.State}'");

// Apply events to transition the instance
var payResult = await client.ApplyEventAsync(new ApplyEventRequest
{
    InstanceId = createResult.InstanceId,
    Event = "PAY",
    Payload = new Dictionary<string, object> { ["amount"] = 99.99 },
});
Console.WriteLine($"Transitioned: {payResult.FromState} -> {payResult.ToState}");

var shipResult = await client.ApplyEventAsync(new ApplyEventRequest
{
    InstanceId = createResult.InstanceId,
    Event = "SHIP",
});
Console.WriteLine($"Transitioned: {shipResult.FromState} -> {shipResult.ToState}");

// Get the current instance state
var instance = await client.GetInstanceAsync(createResult.InstanceId);
Console.WriteLine($"Current state: {instance.State}, machine: {instance.Machine}");
