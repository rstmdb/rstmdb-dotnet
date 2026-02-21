namespace Rstmdb.Client;

/// <summary>
/// Error codes returned by the rstmdb server (SCREAMING_SNAKE_CASE).
/// </summary>
public static class ErrorCodes
{
    public const string UnsupportedProtocol = "UNSUPPORTED_PROTOCOL";
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string AuthFailed = "AUTH_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string MachineNotFound = "MACHINE_NOT_FOUND";
    public const string MachineVersionExists = "MACHINE_VERSION_EXISTS";
    public const string MachineVersionLimitExceeded = "MACHINE_VERSION_LIMIT_EXCEEDED";
    public const string InstanceNotFound = "INSTANCE_NOT_FOUND";
    public const string InstanceExists = "INSTANCE_EXISTS";
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string GuardFailed = "GUARD_FAILED";
    public const string Conflict = "CONFLICT";
    public const string WalIoError = "WAL_IO_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
    public const string RateLimited = "RATE_LIMITED";
}
