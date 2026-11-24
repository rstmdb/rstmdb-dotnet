using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class ReplicationStatusResult
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("primary_sequence")]
    public ulong? PrimarySequence { get; set; }

    [JsonPropertyName("connected_replicas")]
    public int? ConnectedReplicas { get; set; }

    [JsonPropertyName("replicas")]
    public Dictionary<string, object>[]? Replicas { get; set; }

    [JsonPropertyName("upstream")]
    public string? Upstream { get; set; }

    [JsonPropertyName("last_applied_sequence")]
    public ulong? LastAppliedSequence { get; set; }

    [JsonPropertyName("lag_entries")]
    public ulong? LagEntries { get; set; }

    [JsonPropertyName("lag_seconds")]
    public double? LagSeconds { get; set; }
}
