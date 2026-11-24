using System.Text.Json.Serialization;

namespace Rstmdb.Client;

/// <summary>
/// Represents an application-level error from the rstmdb server.
/// </summary>
public class RstmdbException : Exception
{
    [JsonPropertyName("code")]
    public string ErrorCode { get; }

    [JsonPropertyName("retryable")]
    public bool IsRetryable { get; }

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; }

    public RstmdbException(string errorCode, string message, bool isRetryable = false, Dictionary<string, object>? details = null)
        : base(FormatMessage(errorCode, message))
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
        Details = details;
    }

    private static string FormatMessage(string code, string message)
    {
        return string.IsNullOrEmpty(message)
            ? $"rstmdb: {code}"
            : $"rstmdb: {code}: {message}";
    }

    public static bool IsNotFound(Exception ex) => HasCode(ex, ErrorCodes.NotFound);
    public static bool IsInstanceNotFound(Exception ex) => HasCode(ex, ErrorCodes.InstanceNotFound);
    public static bool IsMachineNotFound(Exception ex) => HasCode(ex, ErrorCodes.MachineNotFound);
    public static bool IsInvalidTransition(Exception ex) => HasCode(ex, ErrorCodes.InvalidTransition);
    public static bool IsGuardFailed(Exception ex) => HasCode(ex, ErrorCodes.GuardFailed);
    public static bool IsGuardUnknown(Exception ex) => HasCode(ex, ErrorCodes.GuardUnknown);
    public static bool IsConflict(Exception ex) => HasCode(ex, ErrorCodes.Conflict);
    public static bool IsIdempotencyConflict(Exception ex) => HasCode(ex, ErrorCodes.IdempotencyConflict);
    public static bool IsReadOnlyMode(Exception ex) => HasCode(ex, ErrorCodes.ReadOnlyMode);
    public static bool IsReplicationTimeout(Exception ex) => HasCode(ex, ErrorCodes.ReplicationTimeout);
    public static bool IsReplicationError(Exception ex) => HasCode(ex, ErrorCodes.ReplicationError);

    public static bool CheckRetryable(Exception ex)
    {
        return ex is RstmdbException re && re.IsRetryable;
    }

    private static bool HasCode(Exception ex, string code)
    {
        return ex is RstmdbException re && re.ErrorCode == code;
    }
}
