using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class MachineDefinition
{
    [JsonPropertyName("states")]
    public string[] States { get; set; } = Array.Empty<string>();

    [JsonPropertyName("initial")]
    public string Initial { get; set; } = "";

    [JsonPropertyName("transitions")]
    public Transition[] Transitions { get; set; } = Array.Empty<Transition>();

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Meta { get; set; }
}

public sealed class Transition
{
    [JsonPropertyName("from")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public string[] From { get; set; } = Array.Empty<string>();

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";

    [JsonPropertyName("guard")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Guard { get; set; }
}

/// <summary>
/// Handles JSON "from" being either a single string or an array of strings.
/// Serializes as a single string when length is 1, otherwise as an array.
/// </summary>
public sealed class StringOrArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new[] { reader.GetString()! };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                list.Add(reader.GetString()!);
            }
            return list.ToArray();
        }

        throw new JsonException("Expected string or array of strings");
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        if (value.Length == 1)
        {
            writer.WriteStringValue(value[0]);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var s in value)
                writer.WriteStringValue(s);
            writer.WriteEndArray();
        }
    }
}

public sealed class PutMachineRequest
{
    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("definition")]
    public MachineDefinition Definition { get; set; } = new();

    [JsonPropertyName("checksum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Checksum { get; set; }
}

public sealed class PutMachineResult
{
    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("stored_checksum")]
    public string StoredChecksum { get; set; } = "";

    [JsonPropertyName("created")]
    public bool Created { get; set; }
}

public sealed class MachineInfo
{
    [JsonPropertyName("definition")]
    public MachineDefinition Definition { get; set; } = new();

    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = "";
}

public sealed class MachineSummary
{
    [JsonPropertyName("machine")]
    public string Machine { get; set; } = "";

    [JsonPropertyName("versions")]
    public int[] Versions { get; set; } = Array.Empty<int>();
}
