using System;
using System.Collections.Generic;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents the event arguments for the transport error received event.
/// </summary>
public sealed class TransportErrorReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Enumerates the reasons for transport errors.
    /// </summary>
    public enum TransportErrorReason
    {
        /// <summary>
        /// Indicates that the channel was interrupted.
        /// </summary>
        ChannelInterrupted
    }

    /// <summary>
    /// Reason for the transport error.
    /// </summary>
    public TransportErrorReason ErrorReason { get; set; }

    /// <summary>
    /// List of dropped messages associated with the error.
    /// </summary>
    public IList<string> DroppedMessages { get; set; } = new List<string>();
}