namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Why a publish could not be confirmed by the broker.
/// </summary>
public enum PublishFailureReason
{
    /// <summary>The cause is not known, e.g. a negative confirm or no confirm at all.</summary>
    Unknown,

    /// <summary>The broker could not be reached or the connection was lost.</summary>
    Unreachable,

    /// <summary>The broker refused the message because its destination does not exist.</summary>
    DestinationNotFound,

    /// <summary>The broker refused the message because publishing to its destination is not allowed.</summary>
    AccessRefused,

    /// <summary>The broker refused the message itself, e.g. a property it does not accept.</summary>
    MessageRejected
}