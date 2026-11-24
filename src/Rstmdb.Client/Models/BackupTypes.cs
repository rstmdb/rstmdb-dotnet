using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class BackupBeginResult
{
    [JsonPropertyName("backup_id")]
    public string BackupId { get; set; } = "";

    [JsonPropertyName("manifest")]
    public Dictionary<string, object>? Manifest { get; set; }

    [JsonPropertyName("total_bytes")]
    public ulong TotalBytes { get; set; }

    [JsonPropertyName("chunk_size")]
    public ulong ChunkSize { get; set; }
}

public sealed class BackupChunkResult
{
    [JsonPropertyName("bytes")]
    public string Bytes { get; set; } = "";

    [JsonPropertyName("eof")]
    public bool Eof { get; set; }

    public byte[] DecodeBytes()
    {
        return string.IsNullOrEmpty(Bytes)
            ? Array.Empty<byte>()
            : Convert.FromBase64String(Bytes);
    }
}

public sealed class BackupEndResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }
}
