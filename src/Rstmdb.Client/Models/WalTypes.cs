using System.Text.Json.Serialization;

namespace Rstmdb.Client;

public sealed class SnapshotResult
{
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = "";

    [JsonPropertyName("snapshot_id")]
    public string SnapshotId { get; set; } = "";

    [JsonPropertyName("wal_offset")]
    public ulong WalOffset { get; set; }

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("checksum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Checksum { get; set; }
}

public sealed class WalReadResult
{
    [JsonPropertyName("records")]
    public WalRecord[] Records { get; set; } = Array.Empty<WalRecord>();

    [JsonPropertyName("next_offset")]
    public ulong NextOffset { get; set; }
}

public sealed class WalRecord
{
    [JsonPropertyName("sequence")]
    public ulong Sequence { get; set; }

    [JsonPropertyName("offset")]
    public ulong Offset { get; set; }

    [JsonPropertyName("entry")]
    public object? Entry { get; set; }
}

public sealed class WalStatsResult
{
    [JsonPropertyName("entry_count")]
    public long EntryCount { get; set; }

    [JsonPropertyName("segment_count")]
    public int SegmentCount { get; set; }

    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("latest_offset")]
    public ulong LatestOffset { get; set; }

    [JsonPropertyName("io_stats")]
    public IoStats IoStats { get; set; } = new();
}

public sealed class IoStats
{
    [JsonPropertyName("bytes_written")]
    public ulong BytesWritten { get; set; }

    [JsonPropertyName("bytes_read")]
    public ulong BytesRead { get; set; }

    [JsonPropertyName("writes")]
    public ulong Writes { get; set; }

    [JsonPropertyName("reads")]
    public ulong Reads { get; set; }

    [JsonPropertyName("fsyncs")]
    public ulong Fsyncs { get; set; }
}

public sealed class CompactResult
{
    [JsonPropertyName("snapshots_created")]
    public int SnapshotsCreated { get; set; }

    [JsonPropertyName("segments_deleted")]
    public int SegmentsDeleted { get; set; }

    [JsonPropertyName("bytes_reclaimed")]
    public long BytesReclaimed { get; set; }

    [JsonPropertyName("total_snapshots")]
    public int TotalSnapshots { get; set; }

    [JsonPropertyName("wal_segments")]
    public int WalSegments { get; set; }
}

public sealed class FlushAllResult
{
    [JsonPropertyName("flushed")]
    public bool Flushed { get; set; }

    [JsonPropertyName("instances_removed")]
    public int InstancesRemoved { get; set; }

    [JsonPropertyName("machines_removed")]
    public int MachinesRemoved { get; set; }
}
