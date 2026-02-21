using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class ApplyEventRequest
{
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Payload { get; set; }

    [JsonPropertyName("expected_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedState { get; set; }

    [JsonPropertyName("expected_wal_offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ExpectedWalOffset { get; set; }

    [JsonPropertyName("event_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventId { get; set; }

    [JsonPropertyName("idempotency_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdempotencyKey { get; set; }
}

public sealed class ApplyEventResult
{
    [JsonPropertyName("from_state")]
    public string FromState { get; set; } = "";

    [JsonPropertyName("to_state")]
    public string ToState { get; set; } = "";

    [JsonPropertyName("ctx")]
    public Dictionary<string, object>? Ctx { get; set; }

    [JsonPropertyName("wal_offset")]
    public ulong WalOffset { get; set; }

    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("event_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventId { get; set; }
}
