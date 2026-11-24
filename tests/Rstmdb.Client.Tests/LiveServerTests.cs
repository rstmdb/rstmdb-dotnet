using Xunit;

namespace Rstmdb.Client.Tests;

public class LiveServerTests
{
    [Fact]
    public async Task LiveServer_ParityFlow_WhenEnvConfigured()
    {
        var host = Environment.GetEnvironmentVariable("RSTMDB_HOST");
        if (string.IsNullOrEmpty(host))
            return;

        var portStr = Environment.GetEnvironmentVariable("RSTMDB_PORT");
        int port = int.TryParse(portStr, out var parsed) ? parsed : 7401;

        var opts = new RstmdbOptions
        {
            Auth = Environment.GetEnvironmentVariable("RSTMDB_TOKEN"),
            ClientName = "rstmdb-dotnet-live-test",
        };

        await using var client = await RstmdbClient.ConnectAsync(host, port, opts);
        Assert.Contains("durable_idempotency", client.NegotiatedFeatures);

        var rep = await client.GetReplicationStatusAsync();
        Assert.False(string.IsNullOrEmpty(rep.Role));

        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var machineName = $"dotnet_live_{suffix}";

        var putRes = await client.PutMachineAsync(new PutMachineRequest
        {
            Machine = machineName,
            Version = 1,
            Definition = new MachineDefinition
            {
                States = new[] { "pending", "paid", "shipped" },
                Initial = "pending",
                Transitions = new[]
                {
                    new Transition { From = new[] { "pending" }, Event = "PAY", To = "paid" },
                    new Transition { From = new[] { "paid" }, Event = "SHIP", To = "shipped" },
                },
            },
        });
        Assert.True(putRes.Created);

        var instId = $"dn-inst-{suffix}";
        var createKey = $"dn-ck-{suffix}";
        var cr1 = await client.CreateInstanceAsync(new CreateInstanceRequest
        {
            InstanceId = instId,
            Machine = machineName,
            Version = 1,
            IdempotencyKey = createKey,
        });
        var cr2 = await client.CreateInstanceAsync(new CreateInstanceRequest
        {
            InstanceId = instId,
            Machine = machineName,
            Version = 1,
            IdempotencyKey = createKey,
        });
        Assert.Equal(cr1.WalOffset, cr2.WalOffset);

        var payKey = $"dn-pk-{suffix}";
        var ap1 = await client.ApplyEventAsync(new ApplyEventRequest
        {
            InstanceId = instId,
            Event = "PAY",
            IdempotencyKey = payKey,
        });
        Assert.True(ap1.Applied);

        var ap2 = await client.ApplyEventAsync(new ApplyEventRequest
        {
            InstanceId = instId,
            Event = "PAY",
            IdempotencyKey = payKey,
        });
        Assert.False(ap2.Applied);
        Assert.Equal(ap1.WalOffset, ap2.WalOffset);

        var conflictEx = await Assert.ThrowsAsync<RstmdbException>(() => client.ApplyEventAsync(new ApplyEventRequest
        {
            InstanceId = instId,
            Event = "SHIP",
            IdempotencyKey = payKey,
        }));
        Assert.True(RstmdbException.IsIdempotencyConflict(conflictEx));
        Assert.False(conflictEx.IsRetryable);

        var begin = await client.BackupBeginAsync("gzip");
        ulong downloaded = 0;
        while (true)
        {
            var chunk = await client.BackupChunkAsync(begin.BackupId, downloaded, 1024 * 1024);
            downloaded += (ulong)chunk.DecodeBytes().Length;
            if (chunk.Eof)
                break;
        }
        Assert.Equal(begin.TotalBytes, downloaded);

        var end = await client.BackupEndAsync(begin.BackupId);
        Assert.True(end.Ok);
    }
}
