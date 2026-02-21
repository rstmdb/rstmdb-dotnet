using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class CreateInstanceRequest
{
    [JsonPropertyName("instance_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InstanceId { get; set; }

    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("initial_ctx")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? InitialCtx { get; set; }

    [JsonPropertyName("idempotency_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdempotencyKey { get; set; }
}

public sealed class CreateInstanceResult
{
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("wal_offset")]
    public ulong WalOffset { get; set; }
}

public sealed class Instance
{
    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("ctx")]
    public Dictionary<string, object>? Ctx { get; set; }

    [JsonPropertyName("last_event_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastEventId { get; set; }

    [JsonPropertyName("last_wal_offset")]
    public ulong LastWalOffset { get; set; }
}

public sealed class InstanceSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("last_wal_offset")]
    public ulong LastWalOffset { get; set; }
}

public sealed class InstanceList
{
    [JsonPropertyName("instances")]
    public InstanceSummary[] Instances { get; set; } = Array.Empty<InstanceSummary>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

public sealed class DeleteInstanceResult
{
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    [JsonPropertyName("wal_offset")]
    public ulong WalOffset { get; set; }
}
