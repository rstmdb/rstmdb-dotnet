using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class WatchInstanceRequest
{
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("include_ctx")]
    public bool IncludeCtx { get; set; }

    [JsonPropertyName("from_offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong FromOffset { get; set; }
}

public sealed class WatchAllOptions
{
    [JsonPropertyName("include_ctx")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeCtx { get; set; }

    [JsonPropertyName("from_offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong FromOffset { get; set; }

    [JsonPropertyName("machines")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Machines { get; set; }

    [JsonPropertyName("events")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Events { get; set; }

    [JsonPropertyName("from_states")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? FromStates { get; set; }

    [JsonPropertyName("to_states")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ToStates { get; set; }
}

public sealed class SubscriptionEvent
{
    [JsonPropertyName("subscription_id")]
    public string SubscriptionId { get; set; } = "";

    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("wal_offset")]
    public ulong WalOffset { get; set; }

    [JsonPropertyName("from_state")]
    public string FromState { get; set; } = "";

    [JsonPropertyName("to_state")]
    public string ToState { get; set; } = "";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Payload { get; set; }

    [JsonPropertyName("ctx")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Ctx { get; set; }
}
