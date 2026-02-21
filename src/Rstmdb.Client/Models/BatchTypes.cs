using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rstmdb.Client;

/// <summary>
/// Custom converter for BatchMode to serialize as snake_case strings.
/// </summary>
public sealed class BatchModeConverter : JsonConverter<BatchMode>
{
    public override BatchMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "atomic" => BatchMode.Atomic,
            "best_effort" => BatchMode.BestEffort,
            _ => throw new JsonException($"Unknown BatchMode: {value}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, BatchMode value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            BatchMode.Atomic => "atomic",
            BatchMode.BestEffort => "best_effort",
            _ => throw new JsonException($"Unknown BatchMode: {value}"),
        };
        writer.WriteStringValue(str);
    }
}

[JsonConverter(typeof(BatchModeConverter))]
public enum BatchMode
{
    Atomic,
    BestEffort,
}

public sealed class BatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("params")]
    public object? Params { get; set; }

    public static BatchOperation CreateInstance(CreateInstanceRequest req) =>
        new() { Op = "CREATE_INSTANCE", Params = req };

    public static BatchOperation ApplyEvent(ApplyEventRequest req) =>
        new() { Op = "APPLY_EVENT", Params = req };

    public static BatchOperation DeleteInstance(string instanceId) =>
        new() { Op = "DELETE_INSTANCE", Params = new { instance_id = instanceId } };
}

public sealed class BatchResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BatchError? Error { get; set; }
}

public sealed class BatchError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }
}
