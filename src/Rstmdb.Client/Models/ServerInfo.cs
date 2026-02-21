using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class HelloResult
{
    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("wire_mode")]
    public string WireMode { get; set; } = "";

    [JsonPropertyName("server_name")]
    public string ServerName { get; set; } = "";

    [JsonPropertyName("server_version")]
    public string ServerVersion { get; set; } = "";

    [JsonPropertyName("features")]
    public string[]? Features { get; set; }
}

public sealed class ServerInfo
{
    [JsonPropertyName("server_name")]
    public string ServerName { get; set; } = "";

    [JsonPropertyName("server_version")]
    public string ServerVersion { get; set; } = "";

    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("features")]
    public string[]? Features { get; set; }

    [JsonPropertyName("max_frame_bytes")]
    public int MaxFrameBytes { get; set; }

    [JsonPropertyName("max_batch_ops")]
    public int MaxBatchOps { get; set; }
}
