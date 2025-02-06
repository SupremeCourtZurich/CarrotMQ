namespace CarrotMQ.Core.MessageProcessing.Delivery;

/// <summary>
/// Represents the possible delivery statuses for a message processing result.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// The message was successfully processed and acknowledged.
    /// </summary>
    Ack,
    /// <summary>
    /// The message processing failed, and the message should be rejected.
    /// </summary>
    /// <remarks>If a deadLetter exchange has been configured on the queue, the message will be sent to that exchange</remarks>
    Reject,
    /// <summary>
    /// The message processing encountered a transient issue, and the message should be retried.
    /// </summary>
    Retry
}