namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Why a publish could not be confirmed by the broker.
/// </summary>
public enum PublishFailureReason
{
    /// <summary>The cause is not known, e.g. a negative confirm or no confirm at all.</summary>
    Unknown = 0,

    /// <summary>The broker could not be reached or the connection was lost.</summary>
    Unreachable = 1
}