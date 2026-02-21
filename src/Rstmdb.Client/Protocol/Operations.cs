namespace Rstmdb.Client.Protocol;

internal static class Operations
{
    public const string Hello = "HELLO";
    public const string Auth = "AUTH";
    public const string Ping = "PING";
    public const string Bye = "BYE";
    public const string Info = "INFO";
    public const string PutMachine = "PUT_MACHINE";
    public const string GetMachine = "GET_MACHINE";
    public const string ListMachines = "LIST_MACHINES";
    public const string CreateInstance = "CREATE_INSTANCE";
    public const string GetInstance = "GET_INSTANCE";
    public const string ListInstances = "LIST_INSTANCES";
    public const string DeleteInstance = "DELETE_INSTANCE";
    public const string ApplyEvent = "APPLY_EVENT";
    public const string Batch = "BATCH";
    public const string WatchInstance = "WATCH_INSTANCE";
    public const string WatchAll = "WATCH_ALL";
    public const string Unwatch = "UNWATCH";
    public const string SnapshotInstance = "SNAPSHOT_INSTANCE";
    public const string WalRead = "WAL_READ";
    public const string WalStats = "WAL_STATS";
    public const string Compact = "COMPACT";
}
