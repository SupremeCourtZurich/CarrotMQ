using System;

namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents an exception thrown when the retry limit for a message sent with publisher confirm is exceeded.
/// No ack has been received from the broker to confirm the arrival of this message;
/// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable during the last attempt.
/// </summary>
public sealed class RetryLimitExceededException : PublishFailedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with no specific message.
    /// </summary>
    public RetryLimitExceededException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with a specific message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RetryLimitExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with a specific message and an inner
    /// exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The inner exception that caused this exception.</param>
    public RetryLimitExceededException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with a reason.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="reason">Why the publish was not confirmed.</param>
    /// <param name="inner">The transport's exception that caused this exception, if any.</param>
    public RetryLimitExceededException(string message, PublishFailureReason reason, Exception? inner = null)
        : base(message, reason, inner)
    {
    }
}