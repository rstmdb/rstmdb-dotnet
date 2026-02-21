using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rstmdb.Client.Protocol;

internal sealed class WireRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "request";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

internal sealed class WireResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireError? Error { get; set; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireMeta? Meta { get; set; }
}

internal sealed class WireError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Details { get; set; }
}

internal sealed class WireMeta
{
    [JsonPropertyName("server_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerTime { get; set; }

    [JsonPropertyName("leader")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Leader { get; set; }

    [JsonPropertyName("wal_offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong WalOffset { get; set; }

    [JsonPropertyName("trace_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }
}

internal sealed class WireEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

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

internal sealed class RawMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

internal static class WireSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PeekType(byte[] payload)
    {
        var msg = JsonSerializer.Deserialize<RawMessage>(payload);
        return msg?.Type ?? "";
    }
}
